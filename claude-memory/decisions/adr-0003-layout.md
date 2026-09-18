# ADR 0003 - Folder and namespace layout

**Status:** accepted · **Date:** 2026-09-18

## Context

`SoundCloud/` had grown to 21 files in one folder: wire DTOs, the client, the
options, the JSON context, the logger class and the client-id scraper all
side by side.

## Decision

```
src/Scdl.Core/
  Audio/            codec, protocol, rung, the transcoding catalog
  Results/          ScdlError, Result, Result<T>
  SoundCloud/       client, options, JSON context, errors, stream ranking
    Models/         the api-v2 wire DTOs
    ClientId/       scraping and caching the client_id
  Downloading/      the downloader, naming, request/result contracts
    Hls/            playlist parsing and the ffmpeg muxer
  Tagging/
```

Namespaces follow the folders exactly: `Scdl.Core.SoundCloud.Models`,
`Scdl.Core.SoundCloud.ClientId`, `Scdl.Core.Downloading.Hls`. Moving a folder
without moving the namespace was rejected - a half-applied convention is worse
than either whole one.

## Why these seams and not others

The split is by **rate of change**, not by making the folders even:

- `Models/` changes when **SoundCloud** changes their API.
- `ClientId/` and `Hls/` change when **our** logic changes.

That is a real boundary. Splitting `Downloading/` into `Contracts/` and
`Implementation/` would not be; it would just separate interfaces from the only
things that implement them.

## Vertical Slice Architecture was considered and rejected

VSA solves the problem of *many features* each needing changes across many
layers. scdl has two commands, `get` and `formats`, and they share nearly all
their domain logic. `Features/GetTrack/` and `Features/ListFormats/` would force
`TranscodingCatalog` and `TrackStreams` into a `Shared/` folder - which is
exactly the thing VSA exists to avoid.

The current top level already screams what the project does, which was the
stated goal of Screaming Architecture.

## Other layout rules

- **One public type per file**, named after the type. Enums, records and
  interfaces each get their own file.
- **`internal sealed` by default.** A type goes public only when `Scdl.Cli`
  touches it. `InternalsVisibleTo` covers the test project and
  `DynamicProxyGenAssembly2`, so Moq can still mock internal seams.
- **`[LoggerMessage]` in a separate `<TypeName>Loggers` class**, pulled in with
  `using static`. Keeps the algorithm readable.
- **Constants stay with their only user.** `ApiRoot` lives in
  `SoundCloudClient`; a shared `Constants.cs` would move it away from the one
  place that reads it. `SoundCloudErrors` exists precisely because two types
  branch on those values.

## How to do a move like this again

Move the files, change the namespace declarations, then **build and let the
compiler enumerate every missing `using`**. Four build rounds, each naming
exactly the files still wrong. Do not try to predict the list.
