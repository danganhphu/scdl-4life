---
name: errors-and-exit-codes
description: When a failure is a Result and when it is an exception, and how to add a new failure mode so it reaches the right process exit code. Use this when adding a way for something to go wrong, or when touching Scdl.Core/Results.
---

# Failures in this repo

Two shapes, and the line between them is not about performance.

**`Result<T>` for outcomes that are expected.** SoundCloud advertising a rung it
will not serve; a rung that needs a muxer nobody installed. The caller answers
these by stepping down the ladder, so a `catch` around the loop would hide which
failures are routine and would swallow faults it never meant to.

**Exceptions for faults.** A bad URL, a 401, a full disk, a cancelled run. The
caller cannot do anything sensible with them, and burying one in a return value
only delays the stack trace.

A throw/catch costs tens of microseconds; the HTTP calls around it measured
250-870 ms. Anyone arguing `Result` on performance grounds should be shown those
numbers - the argument is about readability, not speed.

There is no `Maybe<T>`. Nullable reference types are the Maybe and the compiler
enforces them. `Result<T>` earns its place only because it carries the *reason*.
Read it with `TryGetValue`, which keeps null-flow analysis working:

```csharp
if (!streamUri.TryGetValue(out var uri))
{
    attempts.Add(streamUri.Error.Message);

    continue;
}
```

# Adding a new failure mode

1. **Add a member to `ScdlErrorCode`, inside its band.** The bands are 10s the
   request, 20s the ladder, 30s reassembly, 40s access, 50s the local
   environment. `ScdlExitCode.For` maps whole bands, so a member in the wrong
   band silently gets the wrong exit code.
2. **Name it in `ScdlExitCode.For`.** Every member is listed explicitly, even the
   ones that fall to `Failure`, so a new member shows up as a visible omission
   rather than sliding into the discard.
3. **Produce it somewhere.** Either `new ScdlException(message, code)` or a
   factory on `SoundCloudErrors` returning `ScdlError`. A member nobody produces
   is dead vocabulary - the enum is meant to be exhaustive in both directions.
4. **Write the message for a person.** `ScdlException` exists so the CLI can
   print the message bare instead of a stack trace, so it has to read as a
   sentence and say what to do next: *"...and ffmpeg is not on PATH. Install it
   with: winget install Gyan.FFmpeg"*.
5. **Cover the mapping** in `ScdlExitCodeTests` or `ProgramExitCodeTests`.

## The exit codes

| Exit | Meaning |
| --- | --- |
| 0 | the file landed |
| 1 | failed, nothing more specific |
| 2 | the URL or the options were wrong |
| 3 | nothing playable is there |
| 4 | credentials missing or rejected |
| 5 | a local dependency is missing |
| 130 | interrupted, 128 + SIGINT |

The set is deliberately small: many distinct codes are harder to script against
than a few meaningful ones. Detail lives in `ScdlErrorCode`; the exit code only
answers "what should a script do about it". The one invariant that must never
break is that **no failure maps to 0** - `scdl get ... && play` would play
nothing.

`Program.RunAsync` handles exactly three shapes: `ScdlException`,
`OperationCanceledException`, `HttpRequestException`. Anything else escapes with
its stack trace on purpose, because anything else is a bug.
