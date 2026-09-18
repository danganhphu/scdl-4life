# ADR 0004 - ffmpeg is optional, and the ladder steps down

**Status:** accepted · **Date:** 2026-09-18

## Context

HLS playlists have to be reassembled. Handing every playlist to ffmpeg is the
simple answer, but it makes ffmpeg a hard requirement for a tool whose whole job
is downloading one file.

## Decision

Reassemble plain segment playlists in managed code. Hand only **fragmented MP4**
playlists - the ones carrying `#EXT-X-MAP` - to ffmpeg, behind `IMediaMuxer`.

When the best rung needs a muxer and ffmpeg is absent, **step down the ladder**
and say so at Warning level. Do not fail, and do not downgrade silently.

## Why it matters in practice

On a real track: `aac_160k` at the top of the ladder was fragmented MP4, the
next rung `abr_sq` answered 404, and the download only succeeded because the
loop kept going to `mp3_1_0`. A version that stopped at the first obstacle
failed on a perfectly ordinary track.

So "ffmpeg is optional" is accurate but incomplete: it is optional for the MP3
rungs. Getting the AAC rungs on most tracks does need it. Say that plainly
rather than implying the tool is dependency-free.

## Why the warning is loud

The tool exists to be honest about bitrate. Quietly handing someone 128 kbps
when 160 was available, because a dependency was missing, is the exact failure
mode it was built to argue against. The step-down logs at Warning so it reaches
the user without `--verbose`.

## Pinning a format opts out

`--format <preset>` yields exactly one candidate, so an explicit choice is never
silently downgraded - it fails with a message naming what would have been needed.

## Never infer a format from a temporary filename

The first time this path ran end to end with ffmpeg actually installed, it
failed:

```
Unable to choose an output format for '... - NSon Mix.m4a.part';
use a standard extension for the filename or specify the format manually
```

Downloads write to `destination + ".part"`, and ffmpeg picks its muxer from the
extension of the file it is handed. `.part` is not a format it knows, so it
refused to open the output at all.

The same root cause had already been costing something silently: the
`-movflags +faststart` block was guarded by
`Path.GetExtension(outputPath) is ".m4a" or ".mp4"`, which on a `.part` path is
never true. **faststart had never once been applied**, and nothing failed to say
so.

The fix is not a cleverer temporary name. `IMediaMuxer.MuxAsync` now takes the
container extension as its own argument and passes `-f` explicitly, so the
output path and the output format are independent facts. A muxer that reads the
format off a path it did not choose is guessing.

Two things worth remembering beyond this bug:

- **A missing dependency and a broken dependency look identical from the
  outside.** With ffmpeg absent the tool stepped down to 128 kbps MP3; with
  ffmpeg present but the invocation broken it also stepped down, because the
  exception surfaced as a failed rung. The user saw the same file both times and
  reasonably concluded the tool was no better than a web downloader.
- **`winget install` does not update the PATH of a terminal that is already
  open.** `%LOCALAPPDATA%\Microsoft\WinGet\Links` was already on the user PATH,
  but the running shell had a copy from before. Reopening the terminal is the
  fix, and it is worth suspecting before suspecting the code.

Verified against a real track afterwards: `aac_160k`, `major_brand=M4A`,
`bit_rate=160003`, 6:37, cover art intact.
