# SoundCloud api-v2

Undocumented internal API. Everything below was confirmed by running against it,
not by reading a spec, because there is no spec.

## The bitrate ladder

SoundCloud stores **no 320 kbps MP3** and never has. The real rungs, as they
appear in `media.transcodings[].preset`:

| Preset | Real bitrate | Reachable by |
| --- | --- | --- |
| `aac_256k`, `abr_hq` | 256 kbps AAC | Go+ OAuth token only |
| `aac_160k`, `abr_sq` | 160 kbps AAC | anyone, on migrated tracks |
| `mp3_1_0`, `mp3_0_0`, `mp3_0_1`, `mp3_standard` | 128 kbps MP3 | anyone |
| `aac_96k` | 96 kbps AAC | anyone |
| `opus_0_0` | 64 kbps Opus | anyone |

Above 128 there are exactly two honest routes: a Go+ token, or the uploader's
original master through `/tracks/{id}/download`, which only answers when
`downloadable && has_downloads_left`.

Sites advertising "320 kbps SoundCloud downloads" fetch the 128 kbps rung and
re-encode it upwards. This is the premise the whole tool is built on; never add
a code path or a label that implies a bitrate the CDN does not serve.

`TranscodingCatalog.Classify` deliberately keeps an unrecognised preset with a
zero bitrate instead of discarding it. That is how `aac_96k` stayed downloadable
before anyone knew it existed.

## Four traps, all found by downloading a real track

**`abr_sq` is advertised but not served.** It appears in `media.transcodings`
while its `/media/.../stream/hls` endpoint answers 404 - on a track whose
`aac_160k` sibling resolves fine. A dead rung must not end the download; step
down to the next one. This is why `GetStreamUriAsync` returns `Result<Uri>`
rather than throwing.

**AAC over HLS is fragmented MP4.** The playlist carries `#EXT-X-MAP`, so the
segments cannot be concatenated and need a real muxer. MP3 HLS segments
concatenate directly. This is the entire reason ffmpeg is an optional dependency
and not a required one - and it means "ffmpeg is optional" is only true for the
MP3 rungs.

**`on.soundcloud.com` links are 302s and `/resolve` will not follow them.** It
answers 404 instead. Chase the redirect first, then drop the query: it only ever
carries tracking (`utm_*`, `si`) or the playlist the track was opened from
(`in`), none of which `/resolve` wants.

**Track titles often already end in `.mp3`**, because the uploader's file name
became the title. Strip a trailing audio extension before appending the
container extension, or you get `... .mp3.mp3`.

## Other behaviour worth knowing

- `client_id` is never issued. Scrape it from the `a-v2.sndcdn.com/assets/*.js`
  bundles, walking them newest first, and cache it - it rotates on the order of
  months. Cache lives under `%LOCALAPPDATA%\scdl\`.
- The OAuth token is the value after `OAuth ` in the `Authorization` header of
  any api-v2 request in browser devtools. It must be sent as the literal
  `OAuth <token>` scheme, not through `AuthenticationHeaderValue`.
- Playlist payloads inline only the first few tracks in full; the rest are
  id-only stubs that need `/tracks?ids=` in batches of 50. That endpoint does
  **not** preserve playlist order, so the caller has to restore it.
- Date fields are not consistently formatted across endpoints, which is why
  `Track.ReleaseDate` and `CreatedAt` are kept as strings and parsed leniently.
- The CDN is noticeably less cooperative with a default .NET user agent, which
  is why the default in `SoundCloudOptions` looks like a browser.

## Measured latency, for judging optimisations

From a real run: `/resolve` 864 ms, client_id scrape 878 ms, stream endpoint
254 ms. Any micro-optimisation on the failure path is invisible next to these.
This is the number to quote the next time someone proposes avoiding exceptions
on performance grounds.
