# scdl

[![ci](https://github.com/danganhphu/scdl-4life/actions/workflows/ci.yml/badge.svg)](https://github.com/danganhphu/scdl-4life/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/danganhphu/scdl-4life)](LICENSE)

A .NET 10 CLI that downloads a SoundCloud track at the highest bitrate SoundCloud actually serves, and is honest about
what that bitrate is.

## There is no 320 kbps

SoundCloud does not store a 320 kbps MP3. This is the whole ladder, lifted straight from the code that ranks it:

snippet: transcoding-ladder

Sites advertising "320 kbps SoundCloud downloads" fetch the 128 kbps rung and re-encode it upwards: 2.5x the bytes,
slightly worse audio, because lossy re-encoding compounds. Their "WAV" does the same in a lossless container.

Only two routes go above 128 kbps:

- **`aac_256k`**, which needs a Go+ token.
- **The uploader's original master**, which needs the uploader to have enabled downloads. This is the only genuinely
  lossless route, and it arrives in whatever they uploaded - wav, flac, aiff or just an mp3.

`scdl formats <url>` shows which of them a given track offers, without downloading anything.

## Install

Grab the archive for your platform from [Releases](https://github.com/danganhphu/scdl-4life/releases) and unzip it.
The binary is self-contained, so **there is nothing else to install** - no .NET runtime, no SDK.

Windows will show a SmartScreen warning the first time, because the executable is not code signed. *More info* then
*Run anyway*. If that is not acceptable, build it yourself - it is one command, see [Build](#build).

## Use

```powershell
scdl formats "https://soundcloud.com/<user>/<track>"
scdl get "https://soundcloud.com/<user>/<track>" -o D:\Music
scdl get "<url>" --oauth "<go-plus-token>" -o D:\Music
```

`get` also takes a set or playlist URL and expands it. One bad track in a set does not abandon the rest.

| Option                      |                                                                                 |
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

The exit code is the machine-readable output, so a script can tell "install ffmpeg" from "that track is gone" without
parsing English.

| Code | Meaning                                                                    |
|------|------------------------------------------------------------------------------|
| 0    | The file landed.                                                           |
| 1    | Failed, with nothing more specific to say.                                 |
| 2    | The URL or the options were wrong. Re-running unchanged will fail the same way. |
| 3    | Nothing playable is there.                                                 |
| 4    | Credentials are missing or rejected. A Go+ token may be what is needed.    |
| 5    | A local dependency is missing - today that means ffmpeg.                   |
| 130  | Interrupted, the conventional 128 + SIGINT.                                |

### ffmpeg is optional

Only AAC over HLS is fragmented MP4, and only that needs a muxer. Plain segment playlists are reassembled in managed
code. Without ffmpeg installed, `scdl` steps down to the next rung and says so at warning level rather than quietly
handing over worse audio.

```powershell
winget install Gyan.FFmpeg     # Windows
sudo apt install ffmpeg        # Debian, Ubuntu
brew install ffmpeg            # macOS
```

## Build

```powershell
./build.ps1 All
```

Produces a Native AOT binary when the MSVC toolchain is installed, and a trimmed self-contained one otherwise. Other
targets: `Restore`, `Build`, `Test`, `Coverage`, `Publish`, `Run`. On a machine with nothing but the .NET SDK, use
`./build.cmd` or `./build.sh`, which bootstrap PowerShell from the tool manifest.

## How it works

- `client_id` is scraped from the web player bundles and cached for seven days under `%LOCALAPPDATA%\scdl\`. SoundCloud
  publishes no other way to get one.
- SoundCloud advertises rungs it will not serve - `abr_sq` appears in `media.transcodings` and its stream endpoint
  answers 404. That is routine rather than exceptional, so it comes back as a value and the caller steps down the
  ladder instead of failing.
- Downloads write to a `.part` file that is moved into place only on success, so an interrupted run never leaves a
  truncated file that looks complete.
- Parsing is System.CommandLine, rendering is Spectre.Console. `Spectre.Console.Cli` is documented as neither trimmable
  nor AOT appropriate.
- The URL argument has an explicit parser: `Argument<Uri>` resolves its converter through `TypeDescriptor`, which the
  trimmer removes, and the published binary then rejects every URL at run time.

## Contributing

`main` is protected: every change arrives through a pull request, and the three CI jobs have to pass before it can
merge.

```powershell
git switch -c fix/what-this-changes
./build.ps1 Test
git push -u origin fix/what-this-changes
gh pr create --fill
```

The conventions are in [AGENTS.md](AGENTS.md), task-specific procedures in [`.agents/skills`](.agents/skills), and the
reasoning behind the decisions - including approaches that were tried and rejected - in
[`claude-memory`](claude-memory). Pull request titles must be Conventional Commits; a workflow checks that.

Edit `README.source.md`, never `README.md`. The latter is generated during the build, with the code blocks pulled out of
the real sources so the documented ladder cannot drift from the code that implements it.

## Caveat

Downloading a track whose uploader did not enable downloads is against SoundCloud's terms of service. That toggle is how
they express the choice, which is why `--prefer original` exists and why the tool says plainly when the route is closed
rather than working around it.

## License

[MIT](LICENSE).
