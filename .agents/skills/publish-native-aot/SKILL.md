---
name: publish-native-aot
description: Publishing the CLI, the trim and AOT rules every project is held to, and the Visual Studio toolchain traps that break the link step. Use this when publishing, when an IL2xxx/IL3xxx warning appears, or when adding a dependency that reflects.
---

# Publishing scdl

```powershell
./build.ps1 Publish           # Native AOT when MSVC is present
./build.ps1 Publish -NoAot    # trimmed, self-contained, single file
```

The script probes for the MSVC environment itself and falls back rather than
failing. The published binary lands in `artifacts/aot` or `artifacts/trimmed`.

## Why the script exists instead of a plain `dotnet publish`

Visual Studio 2026 breaks the SDK's linker detection two ways:

1. `vswhere -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64` returns
   nothing for v18 even when `link.exe` is present, and that is the query the
   SDK uses. AOT publish then fails with *"Platform linker not found"*.
2. v18 ships `vcvars64.bat` and no `vcvarsall.bat` - and `vcvars64.bat` *calls*
   `vcvarsall.bat`, so it fails outright when the C++ workload is incomplete.

The real fix on a given machine is to install **Desktop development with C++**
from the VS Installer. Note that the architecture argument and the script name
disagree: x64 is passed as `x64` but its script is `vcvars64.bat`.

## Every project is held to the AOT rules

`IsAotCompatible` is on in `Directory.Build.props`, not just on the project that
publishes AOT, so a library reaching for reflection is caught at build time
rather than at publish time. The test projects opt out, because Moq builds
proxies at run time.

`TrimmerSingleWarn=false` plus `TreatWarningsAsErrors` means a single IL warning
fails the publish. That is deliberate: the alternative is shipping a binary that
throws the first time it reaches a reflection path.

## What that rules out

- **`Spectre.Console.Cli`** - documented as neither trimmable nor AOT
  appropriate. `System.CommandLine` does the parsing; Spectre renders only, and
  only its simple renderables (prompts and their type conversion do not survive).
- **`ValidateDataAnnotations()`** - reflects. `[OptionsValidator]` source
  generates an `IValidateOptions<T>` and substitutes non-reflecting attributes.
- **Reflection-based JSON** - `JsonSerializerIsReflectionEnabledByDefault` is
  off; everything goes through `SoundCloudJsonContext`.
- **`Argument<Uri>`** - resolves its converter through `TypeDescriptor`, which
  the trimmer removes. The published binary then rejects *every* URL at run time
  with "Cannot parse argument ... as expected type 'System.Uri'". The parser is
  written by hand in `CommandFactory.CreateUrlArgument`, and
  `UrlArgumentTests` exists because of exactly this.
- **`[Range(TimeSpan)]`** - parses through a TypeConverter. `ClientIdCacheDays`
  is an `int` for that reason.

The lesson behind the `Argument<Uri>` entry is the one worth keeping: a clean
trimmed build means the analyzers found nothing, not that the binary works. Run
the published binary against a real URL before believing it.
