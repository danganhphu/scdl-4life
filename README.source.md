# scdl

A .NET 10 CLI that downloads a SoundCloud track at the highest bitrate SoundCloud actually serves, and is honest about
what that bitrate is.

## There is no 320 kbps

SoundCloud does not store a 320 kbps MP3. This is the whole ladder, lifted straight from the code that ranks it:

snippet: transcoding-ladder

Sites advertising "320 kbps SoundCloud downloads" fetch the 128 kbps rung and re-encode it upwards: 2.5x the bytes,
slightly worse audio, because lossy re-encoding compounds. Their "WAV" does the same in a lossless container.

Only two routes go above 128 kbps: `aac_256k`, which needs a Go+ token, and the uploader's original master, which needs
the uploader to have enabled downloads. `scdl formats <url>` shows which of them a given track offers.

## Build

```powershell
./build.ps1 All
```

Produces a Native AOT binary when the MSVC toolchain is installed, and a trimmed self-contained one otherwise. ffmpeg is
optional: plain HLS playlists are reassembled in managed code, and only fragmented MP4 needs a muxer.

## Use

```powershell
scdl formats "https://soundcloud.com/<user>/<track>"
scdl get "https://soundcloud.com/<user>/<track>" -o D:\Music
scdl get "<url>" --oauth "<go-plus-token>" -o D:\Music
```

| Option                      |                                                                                 |
|-----------------------------|---------------------------------------------------------------------------------|
| `-o`, `--out <dir>`         | Output directory. Defaults to the working directory.                            |
| `-f`, `--format <preset>`   | Force a preset instead of the top rung.                                         |
| `--prefer original\|stream` | `original` (default) takes the uploader's master when offered.                  |
| `--oauth <token>`           | Go+ token, from the `Authorization` header of any `api-v2` request in devtools. |
| `--no-tags`                 | Skip metadata and cover art.                                                    |
| `--overwrite`               | Replace an existing file instead of appending a counter.                        |
| `-v`, `--verbose`           | Log what the client is doing to stderr.                                         |

## Notes

- `client_id` is scraped from the web player bundles and cached for seven days under `%LOCALAPPDATA%\scdl\`. SoundCloud
  publishes no other way to get one.
- Parsing is System.CommandLine, rendering is Spectre.Console. `Spectre.Console.Cli` is documented as neither trimmable
  nor AOT appropriate.
- The URL argument has an explicit parser: `Argument<Uri>` resolves its converter through `TypeDescriptor`, which the
  trimmer removes, and the published binary then rejects every URL at run time.
- Downloads write to a `.part` file that is moved into place only on success, so an interrupted run never leaves a
  truncated file that looks complete.

## Caveat

Downloading a track whose uploader did not enable downloads is against SoundCloud's terms of service. That toggle is how
they express the choice, which is why `--prefer original` exists and why the tool says plainly when the route is closed
rather than working around it.
