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
