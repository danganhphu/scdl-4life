# .NET 10 toolchain traps

Each of these fails **silently**: the build looks fine while doing less than
intended, or the IDE and CI quietly disagree. All were hit in this repo.

## `<WarningsAsErrors>true</WarningsAsErrors>` escalates nothing

`WarningsAsErrors` takes a **list of warning IDs**, plus the special token
`nullable`. `"true"` is parsed as a warning ID that does not exist, so it does
nothing at all. The boolean property is `TreatWarningsAsErrors`.

A repo that writes the first one believes it is strict and is not. This one now
uses `TreatWarningsAsErrors` with `WarningsNotAsErrors` exempting NU1901-NU1904,
so a CVE advisory published against a transitive package does not break an
unrelated build the morning it lands.

## Doc-comment analyzers are dead without `GenerateDocumentationFile`

Without it the compiler never binds doc comments, so RCS1139, RCS1140, RCS1141
and friends never fire on the build - while Rider reports them anyway, because
it analyses doc comments regardless of the MSBuild setting.

**The symptom is an IDE that complains about something CI is happy with.** It is
set in `Directory.Build.props` so projects added later inherit it, with the test
project opting out.

### How this was actually diagnosed, because two attempts failed first

1. First probe appended the rule severities to the **end** of `.editorconfig` -
   which landed inside the `[tests/**/*.cs]` section. The rules applied only to
   the test project, the file under test was in `src/`, and "0 warnings" proved
   nothing. **Check which section an editorconfig line lands in.**
2. Second attempt scanned the analyzer DLLs for the rule id as a byte string.
   Useless: `RCS1161`, which demonstrably *did* fire, appeared in exactly the
   same DLLs. Presence of a string proves nothing about which assembly registers
   the diagnostic.
3. What worked: set the property, rebuild, observe. One variable at a time.

## `dotnet test` opt-in lives in `global.json`, not `dotnet.config`

TUnit runs on Microsoft.Testing.Platform. On the .NET 10 SDK the runner is
selected by:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

in **`global.json`**. A `dotnet.config` file with a `[dotnet.test.runner]`
section does nothing - that was an invention, and it was committed before being
verified. Also: remove `TestingPlatformDotnetTestSupport` from the test project;
it is the older VSTest-bridge opt-in and conflicts with the global.json runner.

In MTP mode the solution goes behind `--solution`. `dotnet test Solution.slnx`
fails with *"Specifying a solution for 'dotnet test' should be via '--solution'"*.

## Visual Studio 2026 breaks Native AOT detection two ways

1. `vswhere -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64` returns
   nothing for v18, even when `link.exe` exists under `VC\Tools\MSVC\`. That is
   the query the SDK uses, so AOT publish fails with "Platform linker not found".
2. v18 ships `vcvars64.bat` and **no `vcvarsall.bat`** - and `vcvars64.bat`
   *calls* `vcvarsall.bat`, so it fails outright when the C++ workload is
   incomplete.

`build.ps1` probes both layouts and imports the environment itself, falling back
to a trimmed self-contained publish when no toolchain is found. The real fix on
a given machine is to install *Desktop development with C++* from the VS
Installer.

Note the script name and the vcvarsall argument disagree: x64 is passed as
`x64` but its script is `vcvars64.bat`, not `vcvarsx64.bat`.

## `MarkdownSnippets.MsBuild` rejects every value for its bool? parameters

`ReadOnly` and `WriteHeader` are declared as `bool?`, and MSBuild on .NET 10
cannot convert a value into that - setting either fails the build with MSB4030.
Leave them unset. `MarkdownSnippetsTargetDirectory` does not exist at all; the
task hardcodes `ProjectDirectory` and finds the repo root itself.

## `LogToStandardErrorThreshold` is on the provider, not the formatter

`AddSimpleConsole(Action<SimpleConsoleFormatterOptions>)` configures the
*formatter*. The threshold lives on `ConsoleLoggerOptions`, so it needs its own
`AddConsole(...)` call. Routing logs to stderr matters here because Spectre owns
stdout and sharing the stream corrupts its live progress display.

## CA1708 fires on two C# 14 extension blocks in one class

A static class holding two `extension(...)` blocks with different receivers
fails the build:

```
CA1708: Names of 'Members' and 'TrackStreams.extension(Track),
TrackStreams.extension(IReadOnlyList<StreamOption>)' should differ by more
than case
```

The compiler emits one grouping type per extension block, and the analyzer sees
two identically named types. Nobody can call them from any language, so the
case-insensitive collision CA1708 exists to prevent cannot happen: it is an
analyzer that has not caught up with the language.

`TrackStreams` suppresses it in `GlobalSuppressions.cs` rather than splitting the
class, because splitting would scatter two operations on the same ladder across
two files to satisfy a tool. Re-check whether the suppression is still needed
after an analyzer update.

## `Assert.That` on a `const` is a build error under TUnit

`TUnitAssertions0005: Assert.That(...) should not be used with a constant value`.
Asserting `ScdlExitCode.Cancelled == 130` reads as pinning a contract, but both
sides are compile-time constants and the analyzer is right that it proves
nothing. Pin the value where it is *used* instead, or leave it to the doc
comment.

## `Uri.TryCreate` disagrees with itself across platforms

`Uri.TryCreate("/artist/track", UriKind.Absolute, out var uri)` returns **false
on Windows and true on Unix**, where a leading slash is an absolute path and
.NET parses it as `file:///artist/track`.

Both platforms still reject the input as a SoundCloud URL, but by different
routes: Windows fails the absolute check, Linux passes it and then fails the
http/https scheme check. A test that asserted the *message* passed locally and
failed only the ubuntu leg of CI.

The lesson is not about `Uri`. It is that **a green Windows run says nothing
about the Linux matrix leg**, and the CI matrix is the only thing that catches
it. Anything that touches paths, path separators or file URIs needs the message
assertion loosened to the behaviour that is actually platform independent.

## `.gitattributes` and `.editorconfig` must agree on line endings

Setting `end_of_line = lf` in `.editorconfig` while `.gitattributes` leaves a
file type at plain `text` means, on a machine with `core.autocrlf=true`, that
the editor writes LF and git hands back CRLF on the next checkout. That is a
phantom diff on every file, forever. This repo pins `* text=auto eol=lf` with
`.cmd`/`.bat` as the only CRLF exception, matching `.editorconfig`.
