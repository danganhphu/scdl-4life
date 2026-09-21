# scdl

[![ci](https://github.com/danganhphu/scdl-4life/actions/workflows/ci.yml/badge.svg)](https://github.com/danganhphu/scdl-4life/actions/workflows/ci.yml)
[![release](https://img.shields.io/github/v/release/danganhphu/scdl-4life)](https://github.com/danganhphu/scdl-4life/releases)
[![license](https://img.shields.io/github/license/danganhphu/scdl-4life)](LICENSE)

Downloads a SoundCloud track at the best bitrate SoundCloud will actually serve, and tells you what that bitrate is.

```text
> scdl formats "https://soundcloud.com/example-artist/example-track"

Example Artist - Example Track
╭──────────┬──────────┬───────┬─────────────┬───────────────────────────────────╮
│ Preset   │  Bitrate │ Codec │ Delivery    │ Note                              │
├──────────┼──────────┼───────┼─────────────┼───────────────────────────────────┤
│ aac_160k │ 160 kbps │ AAC   │ hls         │                                   │
│ abr_sq   │ 160 kbps │ AAC   │ hls         │                                   │
│ mp3_1_0  │ 128 kbps │ MP3   │ hls         │                                   │
│ mp3_1_0  │ 128 kbps │ MP3   │ progressive │                                   │
│ aac_96k  │  96 kbps │ AAC   │ hls         │                                   │
│ original │        - │       │             │ uploader did not enable downloads │
╰──────────┴──────────┴───────┴─────────────┴───────────────────────────────────╯
! 256 kbps AAC is Go+ only. Pass --oauth to check whether it unlocks here.
SoundCloud stores no 320 kbps rung; 256 kbps AAC is the ceiling.
```

## There is no 320 kbps

That is the entire ladder, taken from the code that ranks it:

snippet: transcoding-ladder

Sites selling "320 kbps SoundCloud downloads" take the 128 kbps rung and re-encode it upwards. You get 2.5x the bytes
and slightly worse audio, because lossy re-encoding compounds. Their "WAV" is the same 128 kbps in a lossless container.

Two routes go above 128, and `scdl formats` tells you whether either is open before you download anything:

- `aac_256k`, which needs a Go+ token.
- The uploader's original master, which needs them to have ticked the download box. This is the only lossless route,
  and you get whatever they uploaded - wav, flac, aiff, or an mp3.

## Install

No runtime needed. The binary is self-contained and does not require .NET installed.

```powershell
# Windows: from https://github.com/danganhphu/scdl-4life/releases
Expand-Archive scdl-0.1.0-win-x64.zip -DestinationPath $env:LOCALAPPDATA\scdl
```

```bash
# Linux
tar -xzf scdl-0.1.0-linux-x64.tar.gz -C ~/.local/bin
```

Windows shows a SmartScreen warning the first time, because the binary is not code signed. *More info*, then
*Run anyway*. If that is not acceptable, build it yourself - see [Build](#build).

Every release carries `SHA256SUMS.txt` and a build provenance attestation, so you can check that what you downloaded is
what this repository's workflow built:

```powershell
(Get-FileHash .\scdl-0.1.0-win-x64.zip -Algorithm SHA256).Hash
gh attestation verify .\scdl-0.1.0-win-x64.zip --repo danganhphu/scdl-4life
```

Verify the archive, not the executable inside it. Only the archive's bytes were signed.

## Use

```powershell
scdl formats "https://soundcloud.com/<user>/<track>"
scdl get "https://soundcloud.com/<user>/<track>" -o D:\Music
scdl get "<url>" --oauth "<go-plus-token>" -o D:\Music
```

```text
> scdl get "https://soundcloud.com/example-artist/example-track" -o D:\Music

Example Artist - Example Track
  No original master offered; took the best transcoding instead.
  source  aac_160k (160 kbps AAC)
  saved   D:\Music\Example Artist - Example Track.m4a (7.7 MiB)
```

`get` takes a set or playlist URL too. One dead track does not abandon the rest, and each track is tagged with the set
as its album and with its position in the running order, so a player keeps the uploader's order instead of going
alphabetical.

| Option                      | Meaning                                                                         |
|-----------------------------|---------------------------------------------------------------------------------|
| `-o`, `--out <dir>`         | Output directory. Defaults to the working directory.                            |
| `-f`, `--format <preset>`   | Force a preset instead of the top rung. Never silently downgraded.              |
| `--prefer original\|stream` | `original` (default) takes the uploader's master when offered.                  |
| `--oauth <token>`           | Go+ token, from the `Authorization` header of any `api-v2` request in devtools. |
| `--no-tags`                 | Skip metadata and cover art.                                                    |
| `--overwrite`               | Replace an existing file instead of appending a counter.                        |
| `-v`, `--verbose`           | Log what the client is doing to stderr.                                         |

Logs go to stderr and results to stdout, so `scdl get "<url>" 2>$null` prints only the outcome.

### Exit codes

A script can tell "install ffmpeg" from "that track is gone" without parsing English.

| Code | Meaning                                                                     |
|------|-------------------------------------------------------------------------------|
| 0    | The file landed.                                                            |
| 1    | Failed, with nothing more specific to say.                                  |
| 2    | The URL or the options were wrong. Re-running unchanged fails the same way. |
| 3    | Nothing playable is there.                                                  |
| 4    | Credentials missing or rejected. A Go+ token may be what is needed.         |
| 5    | A local dependency is missing - today that means ffmpeg.                    |
| 130  | Interrupted. The conventional 128 + SIGINT.                                 |

### ffmpeg is optional

AAC over HLS arrives as fragmented MP4 and needs a muxer; everything else is reassembled in managed code. Without
ffmpeg, `scdl` steps down to the next rung and says so at warning level rather than quietly handing you worse audio.

```powershell
winget install Gyan.FFmpeg     # Windows
sudo apt install ffmpeg        # Debian, Ubuntu
brew install ffmpeg            # macOS
```

## Notes from the api-v2 side

It is undocumented and inconsistent, so a few things are worth knowing before reading the code:

- `client_id` is scraped from the web player bundles and cached for seven days under `%LOCALAPPDATA%\scdl\`. SoundCloud
  publishes no other way to get one.
- SoundCloud advertises rungs it will not serve. `abr_sq` sits in `media.transcodings` while its stream endpoint
  answers 404, so a dead rung steps down the ladder instead of ending the download.
- `on.soundcloud.com` short links are 302s that `/resolve` refuses to follow.
- Downloads write to a `.part` file that moves into place only on success, so an interrupted run leaves nothing that
  looks complete.

## Set up

The .NET 10 SDK is the only hard requirement - `global.json` pins the version and selects the test runner, and
everything else is restored from the repository.

```powershell
git clone https://github.com/danganhphu/scdl-4life.git
cd scdl-4life
./build.ps1 All
```

`build.ps1` is the engine and the other entry points forward to it: `Restore`, `Build`, `Test`, `Coverage`, `Publish`,
`Run`. On a machine with nothing but the SDK, start with `./build.cmd` or `./build.sh` instead - they bootstrap
PowerShell out of the tool manifest, which is what makes a fresh clone work when only Windows PowerShell 5.1 is
present, or when the execution policy refuses an unsigned script.

Two optional pieces, neither of which blocks a build or the tests:

- **ffmpeg** on PATH, for the AAC rungs. The muxer is mocked in the tests, so a green suite says nothing about whether
  you have it.
- **Desktop development with C++** from the Visual Studio installer, for Native AOT. Without it `./build.ps1 Publish`
  falls back to a trimmed self-contained binary; CI links AOT on both platforms either way.

Agent instructions are installed rather than committed, because they ship inside the Aspire CLI and are version matched
to it. Run this after cloning, and again after `aspire update --self`:

```powershell
aspire agent init --skills all --skill-locations claudecode
```

## Contributing

`main` is protected: every change goes through a pull request with the CI jobs green.

```powershell
git switch -c fix/what-this-changes
./build.ps1 Test
git push -u origin fix/what-this-changes
gh pr create --fill
```

Conventions live in [AGENTS.md](AGENTS.md), task-specific procedures in [`.agents/skills`](.agents/skills). Pull
request titles are Conventional Commits and a workflow checks that.

Edit `README.source.md`, never `README.md`. The latter is generated during the build, with the ladder pulled out of the
real source so it cannot drift from the code.

## One caveat

Downloading a track whose uploader did not enable downloads is against SoundCloud's terms of service. That toggle is how
they express the choice, which is why `--prefer original` exists and why the tool says plainly when the route is closed
instead of working around it.

## License

[MIT](LICENSE).
