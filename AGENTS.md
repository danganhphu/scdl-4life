# AGENTS.md

Conventions for anyone, human or agent, working in this repo. Keep it current: if a rule here stops matching the code,
one of the two is wrong.

## What this is

A .NET 10 CLI that downloads a SoundCloud track at the highest bitrate SoundCloud actually serves, and is honest about
what that bitrate is. The honesty is the product: SoundCloud stores no 320 kbps rung, and the tool exists partly to stop
people paying for sites that pretend otherwise. Never add a code path, a label or a doc line that implies a bitrate the
CDN does not serve.

| Project                 | Role                                                                                |
|-------------------------|-------------------------------------------------------------------------------------|
| `src/Scdl.Core`         | Resolving, ranking, downloading, tagging. No console dependency. Ships XML docs.    |
| `src/Scdl.Cli`          | System.CommandLine parsing, Spectre.Console rendering. Publishes Native AOT.        |
| `tests/Scdl.Core.Tests` | TUnit, Moq, Bogus, plus architecture tests over the Core assembly.                  |
| `tests/Scdl.Cli.Tests`  | Parsing and rendering, through Spectre's `TestConsole`. No network, no mock console. |

## Commands

`build.ps1` is the engine; everything else in the root forwards to it.

```powershell
./build.ps1 Restore    # dotnet tool restore + package restore
./build.ps1 Build      # restore + build Release
./build.ps1 Test       # build, then dotnet test
./build.ps1 Coverage   # dotnet-coverage + reportgenerator into artifacts/coverage
./build.ps1 Publish    # Native AOT when MSVC is present, trimmed self-contained otherwise
./build.ps1 Run -- formats "<url>"
./build.ps1 All
```

`restore`/`build`/`test`/`run` also exist as `.ps1`, `.cmd` and `.sh`. The `.ps1` ones are three-line forwarders for
convenience. The `.cmd` and `.sh` ones are bootstrappers, and they exist for a real reason: `cmd.exe` cannot execute a
`.ps1` at all, PowerShell's execution policy can refuse an unsigned freshly cloned script, and a new machine may only
have Windows PowerShell 5.1, which `#Requires -Version 7.0` rejects. They run `dotnet tool run pwsh`, taking PowerShell
from `.config/dotnet-tools.json`, so a clone with nothing but the .NET SDK installed can still build.

`dotnet test` works directly too. TUnit runs on Microsoft.Testing.Platform, and the runner is selected by the `"test"`
section of `global.json`. Do not add `TestingPlatformDotnetTestSupport` to a test project: that is the older
VSTest-bridge opt-in, it conflicts with the global.json runner, and the .NET 10 SDK refuses the combination.

## Style

- **One public type per file**, named after the type. Enums, records and interfaces each get their own file.
- **`internal sealed` by default.** A type goes public only when `Scdl.Cli` actually touches it. `InternalsVisibleTo`
  covers the test project and `DynamicProxyGenAssembly2` so Moq can still mock internal seams.
- **`[LoggerMessage]` declarations live in a separate `<TypeName>Loggers` static partial class**, and the consuming type
  pulls them in with `using static`. Keeps the algorithm readable and keeps the generator's cached delegates.
- **Primary constructors** for injected dependencies. Only keep an explicit field when the parameter is transformed,
  such as `IOptions<T>.Value`.
- **`nameof` over string literals** anywhere the string names a member.
- **C# 14 extension members** where the call reads as a question about a value that already exists -
  `track.RankStreams()`, not `TrackStreams.Rank(track)`. The model types stay plain deserialization targets with no
  behaviour. Two `extension(...)` blocks in one class currently trip CA1708, suppressed in `GlobalSuppressions.cs` with
  the reasoning.
- **One literal per fact.** A string repeated at three call sites is three chances to get it wrong -
  `AudioFileExtensions` exists for exactly that reason. A constant with a single user does not need a home.
- Prefer `IReadOnlyList<T>` on public surfaces, `FrozenDictionary`/`FrozenSet` for lookup tables built once.
- ASCII punctuation only in code, comments and commit messages: hyphens, straight quotes, no ellipsis character.
- Comments explain **why**, not what. A comment restating the line below it is noise; a comment naming the trap that
  made the line necessary is the point.

## Analyzers are not advisory

`Directory.Build.props` sets `TreatWarningsAsErrors`, so every compiler and analyzer warning fails the build. The only
exceptions are the NuGet audit codes NU1901-NU1904, listed in `WarningsNotAsErrors`: a CVE advisory published against a
transitive package is not a reason for an unrelated build to break that morning, and it stays visible as a warning.

Watch the property name. `TreatWarningsAsErrors` is the boolean; `WarningsAsErrors` takes a *list* of warning IDs plus
the token `nullable`, so `<WarningsAsErrors>true</WarningsAsErrors>` escalates nothing at all because "true" is not a
warning ID.

On top of that: `AnalysisMode=Recommended`, `AnalysisLevel=latest`, and Roslynator escalated by category rather than
rule by rule. Because warnings are already errors, the category line is the whole enforcement; there is no list of
individual rule severities to keep in sync.

A build must end at **0 warnings**. Fix the finding rather than suppressing it; when a rule genuinely does not apply,
turn it off in `.editorconfig` with a comment saying why.

`GenerateDocumentationFile` is on for every project except the tests. Without it the compiler never binds doc comments
and the doc-comment analyzers silently do nothing on the build while the IDE still reports them.

## Trimming and AOT

`Scdl.Cli` publishes Native AOT, so the whole dependency graph has to survive trimming. Before adding a package, check
it does not resolve types through reflection.

Known landmines, all of them already paid for once:

- `Spectre.Console.Cli` is not trimmable and not AOT appropriate. Parsing stays on System.CommandLine; Spectre is for
  rendering only.
- `Argument<Uri>` resolves its converter through `TypeDescriptor`, which the trimmer removes, so the published binary
  rejects every URL at run time. Command-line arguments that are not primitives need an explicit `CustomParser`.
- JSON goes through the source-generated `SoundCloudJsonContext`, and `JsonSerializerIsReflectionEnabledByDefault` is
  false so a forgotten `[JsonSerializable]` fails loudly instead of falling back to reflection.
- Tagging is ATL rather than TagLibSharp because ATL parses containers in managed code.

## SoundCloud specifics

api-v2 is undocumented and inconsistent. Things that are true and were expensive to learn:

- `client_id` is scraped from the web player bundles and cached; it is not issued.
- SoundCloud advertises rungs it will not serve. `abr_sq` appears in `media.transcodings` and its stream endpoint
  answers 404. A dead rung must not end a download while others remain.
- AAC over HLS is fragmented MP4 and needs a muxer. MP3 segments concatenate directly. That is the whole reason ffmpeg
  is optional rather than required.
- `on.soundcloud.com` links are 302s that `/resolve` will not follow.
- Track titles often already end in `.mp3`, so the file name needs the extension stripped before the container extension
  is added.

## Tests

TUnit with `await Assert.That(...)`. Test names are sentences with underscores; CA1707 is off for the test project only.
Moq is for real interface seams; HTTP is stubbed with a hand-written `HttpMessageHandler`, because mocking a protected
method by string name breaks silently when the signature moves.

Architecture tests in `ArchitectureTests.cs` anchor on `ICoreAssemblyMarker` and assert the shape of the public surface.
If one fails, decide whether the rule or the code is wrong before editing either.

## Commits

Plain Conventional Commits: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`, `build:`, `ci:`, optional scope,
imperative lowercase subject. A body when the change needs a why.

**No AI attribution trailers.** No `Co-Authored-By` for a tool, no "generated with" line.

## Docs

`README.md` is generated from `README.source.md` by MarkdownSnippets during the build. Edit the source, never the
output. Code blocks come from real files through `snippet:` references so the documented bitrate ladder cannot drift
from the code that implements it.

## Instructions for agents

This file is the always-on part. Task-specific procedures live in `.agents/skills`, one folder per skill with a
`SKILL.md` carrying `name` and `description` frontmatter. `CLAUDE.md` and `.github/copilot-instructions.md` both forward
here, so every assistant reads one set of rules.

| Skill | Read it before |
|-----------------------|------------------------------------------------------------------------------|
| `soundcloud-ladder`   | touching `Scdl.Core/Audio`, or answering anything about 320 kbps or "lossless" |
| `errors-and-exit-codes` | adding a way for something to go wrong                                      |
| `tunit-testing`       | adding or changing a test                                                     |
| `publish-native-aot`  | publishing, or adding a dependency that reflects                              |
| `analyzer-gates`      | an analyzer blocks the build                                                  |

MCP servers are declared twice on purpose: `.mcp.json` for Claude Code and anything else reading the standard
project-scoped format, `.vscode/mcp.json` for VS Code, which only reads its own. Keep them in step.

### Aspire guidance is installed, not committed

```powershell
aspire agent init --skills all --skill-locations claudecode
```

That writes Aspire's own skills into `.claude/skills/`, which is gitignored. They ship inside the Aspire CLI and are
version-matched to it, so a vendored copy would be wrong for anyone on a different CLI version. Run the command after
cloning, and again after `aspire update --self`.

Two things the command does that are worth knowing:

- It installs a **telemetry hook into the user-level `~/.claude/settings.json`**, not into this repo, so it applies to
  every project on that machine. Remove the hook or set `ASPIRE_CLI_TELEMETRY_OPTOUT=true` if that is not wanted.
- The Aspire MCP server is declared in `.mcp.json` as `aspire agent mcp`. It exposes the running AppHost's resources,
  logs and traces, and it also serves the current aspire.dev documentation through `search_docs` and `get_doc` - which
  is the right way to answer an Aspire question, rather than from memory.

There is also a `claude-memory/` folder on the maintainer's machine, holding the long form of every decision. It is
**not** in git and nothing here depends on it. Anything a contributor needs belongs in this file or in a skill; if a
rule seems to be missing its reason, the reason is missing from here and should be added.
