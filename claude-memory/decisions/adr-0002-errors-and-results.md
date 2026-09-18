# ADR 0002 - Result<T> for expected outcomes, exceptions for faults

**Status:** accepted · **Date:** 2026-09-18

## Context

The rung loop in `TrackDownloader` had grown a `catch (ScdlException or
HttpRequestException) { ...; continue; }` and a `long?` used as a poor man's
Option. Both are exceptions-as-control-flow, and both appeared because *some*
failures here are entirely routine: SoundCloud advertises rungs it will not
serve, and a rung may need a muxer that is not installed.

## Decision

`Result<T>` (`readonly record struct`) for outcomes that are **expected**.
Exceptions for **faults**.

The boundary:

| Situation | Shape |
| --- | --- |
| Rung advertised but answers 404 | `Result<Uri>` failure |
| Transcoding entry has no endpoint | `Result<Uri>` failure |
| Fragmented MP4 with no muxer | `long?` from `TryDownloadHlsAsync` |
| Uploader did not enable downloads | `Uri?` from `TryGetOriginalUriAsync` |
| Bad URL, 401, disk full, cancelled | throw |

## Why not just exceptions

Not performance. A throw/catch is tens of microseconds; the HTTP calls around it
measured 250-870 ms. Anyone arguing Result on performance grounds here should be
shown those numbers.

The reason is that a `catch` around a loop body hides which failures are routine
and which are not, and it catches faults it never meant to.

## Why not a full Result everywhere

`ISoundCloudClient.ResolveAsync` still throws, because a bad URL ends the run -
there is nothing to recover to. Threading `Result` through six layers so that
`Program.Main` can unwrap it and print the same message is more code for the
same behaviour.

## Why no Maybe<T>

Nullable reference types **are** the Maybe, and the compiler enforces them.
Adding `Maybe<T>` would be a second way to say "might not have one" that the
compiler does not understand.

`Result<T>` earns its place because it carries the **reason**, which `T?` cannot.
The design point that makes it safe is `TryGetValue` with
`[MaybeNullWhen(false)]`: the compiler still knows the value is non-null in the
success branch. That answers the main objection to Result types in C#.

## Error codes are an enum, and they map to exit codes

Added after the first cut. `ScdlError.Code` started as a `string`, with the
values in a `SoundCloudErrorCodes` static class. That is now
`Results/ScdlErrorCode.cs`, an enum, and `ScdlException` carries one too - so a
failure reads the same whether it was thrown or returned.

The prompt was a2a-dotnet's pair: `A2AErrorCode` plus `A2AErrorCodeMapping`,
which maps the enum onto HTTP status and gRPC status. A CLI has no HTTP status.
Its machine-readable output is the **process exit code**, so the analogue is
`Results/ScdlExitCode.cs`:

| Exit | Meaning | Example code |
| --- | --- | --- |
| 0 | the file landed | - |
| 1 | failed, nothing more specific | `MuxerFailed`, `ClientIdUnavailable` |
| 2 | the URL or options were wrong | `UnsupportedResource`, `PresetNotOffered` |
| 3 | nothing playable is there | `NotFound`, `NoPlayableStream`, `RungNotServed` |
| 4 | credentials missing or rejected | `Unauthorized` |
| 5 | a local dependency is missing | `MuxerUnavailable` |
| 130 | interrupted (128 + SIGINT) | - |

Two things are deliberate:

- **The enum is banded in tens by cause** (10s the request, 20s the ladder, 30s
  reassembly, 40s access, 50s the local environment) so the mapping reads as
  whole groups rather than as eighteen separate arms. Keep a new member inside
  its band.
- **The exit-code set is small.** Many distinct codes are harder to script
  against than a few meaningful ones. The enum is where the detail lives; the
  exit code only answers "what should a script do about it".

`ScdlExitCodeTests` pins the invariant that matters: no failure ever maps to 0,
or `scdl get ... && play` would play nothing.

## Notes from building it

Two analyzer findings shaped the final shape, both correct:

- **CA1716** - `Error` is a reserved word in Visual Basic. Renamed `ScdlError`.
- **CA1000** - no static members on generic types. `Success`/`Failure` moved to
  a non-generic `Result` class, the way `Task`/`Task<T>` do it.
