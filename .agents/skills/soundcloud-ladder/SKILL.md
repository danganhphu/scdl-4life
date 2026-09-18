---
name: soundcloud-ladder
description: The bitrates SoundCloud actually serves, and how to verify a claim about one. Use this before changing anything under Scdl.Core/Audio, before adding or renaming a preset, and whenever a request or an issue mentions 320 kbps, WAV, lossless or "high quality".
---

# The SoundCloud transcoding ladder

This tool exists because the answer to "can I get 320 kbps from SoundCloud" is
**no**, and every site claiming otherwise re-encodes 128 kbps upward. Never add a
code path, a label, a default or a doc line that implies a bitrate the CDN does
not serve.

## What `media.transcodings` actually offers

| Preset | Real kbps | Codec | Container | Gate |
| --- | --- | --- | --- | --- |
| `aac_256k`, `abr_hq` | 256 | AAC | `.m4a` | Go+ token |
| `aac_160k`, `abr_sq` | 160 | AAC | `.m4a` | free |
| `mp3_1_0`, `mp3_0_1`, `mp3_0_0`, `mp3_standard` | 128 | MP3 | `.mp3` | free |
| `aac_96k` | 96 | AAC | `.m4a` | free |
| `opus_0_0` | 64 | Opus | `.ogg` | free |

Above 128 kbps there are exactly two honest routes: `aac_256k` with a Go+ OAuth
token, and the uploader's original master when they enabled downloads. The
master is the only genuinely lossless option and it is whatever they uploaded -
wav, flac, aiff or just an mp3.

## Four things that are only learnable by downloading a real track

1. **`abr_sq` is advertised and then 404s.** It appears in `media.transcodings`
   but its `/stream` endpoint refuses to serve. This is routine, not an error,
   and it is the reason `GetStreamUriAsync` returns `Result<Uri>` instead of
   throwing - the caller steps down the ladder.
2. **AAC HLS is fragmented MP4.** The playlist carries `#EXT-X-MAP`, so the
   segments cannot be concatenated and need a real muxer. That is the only thing
   ffmpeg is for here, which is why it stays an optional dependency: plain
   segment playlists are concatenated in managed code.
3. **`on.soundcloud.com` short links must be followed manually.** The 302 is not
   followed by default on the resolve call; resolve the final URL first.
4. **A stream URL is signed and short lived.** Fetch it immediately before
   transferring, never up front for a whole set.

## How to verify a claim before writing code for it

Do not reason about it. Run:

```powershell
./build.ps1 Run -- formats "<track url>"
```

`formats` prints the rungs the API offered for that exact track and exits
without downloading. If a preset is not in that table, it is not available,
whatever a downloader site says.

## When changing the ladder

`TranscodingCatalog.ByPreset` is the single source. `AllRungs`,
`FreeTierCeilingKbps` and `LadderCeilingKbps` derive from it, `README.md`
regenerates from it through MarkdownSnippets, and `TranscodingCatalogTests`
asserts `Ladder_has_no_320_kbps_rung`. Extensions come from
`AudioFileExtensions`, never from a literal.
