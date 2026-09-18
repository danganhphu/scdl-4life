---
name: tunit-testing
description: How tests are written and run in this repo - TUnit on Microsoft.Testing.Platform, Moq for interface seams, hand-written stubs for HttpMessageHandler, real temp directories for file behaviour. Use this when adding or changing a test, or when dotnet test behaves unexpectedly.
---

# Testing in this repo

Two projects: `tests/Scdl.Core.Tests` (domain, downloading, architecture) and
`tests/Scdl.Cli.Tests` (parsing, rendering, exit codes). Both are `Exe`, both opt
out of the AOT analyzers, both `NoWarn` CA1707 so test names can be sentences.

## Running them

```powershell
./build.ps1 Test                                          # restore, build, test
dotnet test --solution scdl-4life.slnx -c Release --no-build
```

TUnit runs on Microsoft.Testing.Platform, selected by the `"test"` section of
`global.json`. Two rules follow from that:

- The solution goes behind `--solution`. A bare positional path fails with
  *"Specifying a solution for 'dotnet test' should be via '--solution'"*.
- Never add `TestingPlatformDotnetTestSupport` to a test project. It is the older
  VSTest-bridge opt-in and the SDK refuses the combination.

## Shape of a test

```csharp
[Test]
[Arguments("aac_256k", 256, AudioCodec.Aac)]
public async Task Classify_maps_known_presets_to_their_real_bitrate(string preset, int kbps, AudioCodec codec)
{
    var rung = TranscodingCatalog.Classify(preset, mimeType: null);

    await Assert.That(rung.Kbps).IsEqualTo(kbps);
}
```

- `[Arguments]`, not `[InlineData]`. Every assertion is awaited.
- Exceptions: `var e = await Assert.ThrowsAsync<ScdlException>(async () => ...)`,
  then assert on `e!.Code` rather than on the message where a code exists.
- **`Assert.That` on a compile-time constant is a build error**
  (`TUnitAssertions0005`). Asserting `ScdlExitCode.Cancelled == 130` proves
  nothing; pin the value where it is used instead.
- Test names are sentences describing the behaviour, not the method under test.
  The doc comment says *why the behaviour matters*, not what the code does.

## Choosing a fake

| Seam | Use | Why |
| --- | --- | --- |
| A real interface (`ISoundCloudClient`, `IMediaMuxer`) | Moq, `MockBehavior.Strict` | An unexpected call should fail the test |
| `HttpMessageHandler` | `Fakes/StubHttpMessageHandler` | `SendAsync` is protected; mocking it matches by string name and breaks silently |
| The filesystem | `Fakes/TempDirectory`, a real directory | The `.part` write, the move into place and the cleanup after a failure *are* the behaviour; an `IFileSystem` seam would only test the seam |
| `IProgress<T>` | a hand-written recorder | `Progress<T>` posts through the synchronization context, so ticks are still in flight when the assertions run |
| `TimeProvider` | `Fakes/FixedTimeProvider` | - |

Moq needs `InternalsVisibleTo("DynamicProxyGenAssembly2")` to proxy an internal
interface; it is already there.

## Does the test have teeth

A test that passes the first time has not been shown to fail. For anything
load-bearing, break the code on purpose, watch the one expected test go red,
then `git checkout --` the file. The segment-ordering test was verified that way.

## Architecture tests

`ArchitectureTests` asserts rules a reviewer would otherwise have to remember:
every public type under the root namespace, every public concrete class sealed,
no ATL type on a public signature. Adding a public type that breaks one of those
means either the type or the rule is wrong - decide which before suppressing.
