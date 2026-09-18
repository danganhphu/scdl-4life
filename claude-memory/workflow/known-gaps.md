# Known gaps

Honest list of what is not done, as of 2026-09-18. Update it when one closes.

## Not finished

**The original-master path has no test.** `TryDownloadOriginalAsync` is the one
branch of `TrackDownloader` still uncovered: the HLS path, the ladder step-down
and the `.part` cleanup are all tested, but the lossless route needs a track
whose uploader enabled downloads, and the tests never stub that far. The
`FileNaming.ExtensionFor` mapping it depends on is covered separately, so the
untested part is the wiring, not the logic.

**Native AOT has never actually linked on this machine.** The managed side is
clean: `IsAotCompatible` is on for every project, a trimmed publish reports zero
IL warnings, and ILC gets as far as linking. It stops at "Platform linker not
found" because the *Desktop development with C++* workload is missing from
Visual Studio 2026. `build.ps1` falls back to a trimmed self-contained binary
(19 MB) which is what has been tested end to end. Install the workload and run
`./build.ps1 Publish` to close this.

**`--oauth` has never been exercised.** No Go+ account was available, so the
256 kbps AAC path is written but unproven. Everything about it - the `OAuth
<token>` header scheme, whether `aac_256k` then appears in `media.transcodings`
- is inferred rather than observed.

**ffmpeg is not installed here.** So the fragmented-MP4 path through
`FfmpegMuxer` has never run. The step-down that happens instead is well tested;
the muxing itself is not.

**Coverage has a task but no gate.** `./build.ps1 Coverage` produces a report;
nothing enforces a threshold and CI does not publish it.

## Deliberately not done

These were considered and rejected. Read the reasoning before re-proposing.

- **Vertical Slice Architecture** - see `decisions/adr-0003-layout.md`.
- **`Maybe<T>`** - nullable reference types already are it, with compiler
  support a custom type cannot match. See `decisions/adr-0002`.
- **A shared `Constants.cs`** - every constant currently has exactly one user.
- **More design patterns for their own sake** - factory, strategy, decorator and
  adapter are all already present where they solve something.
- **`UsePublicApiAnalyzers`** - Aspire uses it; `Scdl.Core` is not shipped as a
  package, so tracking its public API surface would be ceremony.
- **CSharpier** - would fight the hand-tuned `.editorconfig`.
- **Vendoring third-party skills into `.claude/skills/`** - the skills in use
  are user-level plugins and already apply in every directory.

## Open question

`.ps1` files are indented four spaces to match the PowerShell community style,
while the markup files use two. Rider's PowerShell formatter also uses Allman
braces here. That combination is deliberate but was only settled once; if
`Ctrl+Alt+L` ever starts fighting the files, this is the thing to revisit.
