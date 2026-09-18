using Scdl.Core.Audio;
using Scdl.Core.Downloading.Hls;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;
using static Scdl.Core.Downloading.TrackDownloaderLoggers;

namespace Scdl.Core.Downloading;

/// <summary>Picks the best available source for a track and streams it to disk.</summary>
internal sealed class TrackDownloader(
    HttpClient http,
    ISoundCloudClient soundCloud,
    IMediaMuxer muxer,
    IOptions<SoundCloudOptions> options,
    ILogger<TrackDownloader> logger) : ITrackDownloader
{
    private const int CopyBufferSize = 128 * 1024;
    private const string PartialSuffix = ".part";

    private readonly SoundCloudOptions _options = options.Value;

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Directory.CreateDirectory(request.OutputDirectory);

        var stem = FileNaming.BuildStem(request.Track);

        if (request.Prefer is SourcePreference.Original)
        {
            var original = await TryDownloadOriginalAsync(request, stem, progress, cancellationToken)
                               .ConfigureAwait(false);

            if (original is not null)
            {
                return original;
            }

            LogNoOriginal(logger, request.Track.Id);
        }

        return await DownloadTranscodingAsync(
                         request,
                         stem,
                         fellBackFromOriginal: request.Prefer is SourcePreference.Original,
                         progress,
                         cancellationToken)
                     .ConfigureAwait(false);
    }

    /// <summary>
    /// The only route to genuinely lossless audio, and only when the uploader
    /// enabled downloads. Returns null when that route is closed.
    /// </summary>
    private async Task<DownloadResult?> TryDownloadOriginalAsync(
        DownloadRequest request,
        string stem,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var originalUri = await soundCloud
                                .TryGetOriginalUriAsync(request.Track, cancellationToken)
                                .ConfigureAwait(false);

        if (originalUri is null)
        {
            return null;
        }

        using var response = await http
                                   .GetAsync(originalUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                   .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // The extension is only knowable from the response, because the original
        // is whatever the uploader submitted: wav, flac, aiff or mp3.
        var destination = ResolveDestination(request, stem, FileNaming.ExtensionFor(response));
        var bytes = await WriteResponseAsync(response, destination, progress, cancellationToken)
                          .ConfigureAwait(false);

        return new DownloadResult
        {
            FilePath = destination,
            Source = DownloadSource.OriginalMaster,
            Bytes = bytes,
        };
    }

    private async Task<DownloadResult> DownloadTranscodingAsync(
        DownloadRequest request,
        string stem,
        bool fellBackFromOriginal,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var candidates = SelectCandidates(request.Track, request.Preset);
        var attempts = new List<string>(candidates.Count);

        // No try/catch here on purpose. Stepping down the ladder is an expected
        // outcome, not an error, so the two ways it happens - a rung SoundCloud
        // advertises but will not serve, and fragmented MP4 with no muxer - come
        // back as values. Anything that does throw out of this loop is a real
        // fault and should end the download.
        foreach (var option in candidates)
        {
            var streamUri = await soundCloud
                                  .GetStreamUriAsync(request.Track, option.Transcoding, cancellationToken)
                                  .ConfigureAwait(false);

            if (!streamUri.TryGetValue(out var uri))
            {
                LogRungSkipped(logger, option.Rung.Preset, streamUri.Error.Message);
                attempts.Add(streamUri.Error.Message);

                continue;
            }

            var destination = ResolveDestination(request, stem, option.Rung.FileExtension);

            var bytes = option.Protocol switch
            {
                DeliveryProtocol.Progressive =>
                    await DownloadProgressiveAsync(uri, destination, progress, cancellationToken)
                        .ConfigureAwait(false),

                DeliveryProtocol.Hls =>
                    await TryDownloadHlsAsync(uri, destination, progress, cancellationToken)
                        .ConfigureAwait(false),

                _ => throw new ScdlException(
                         $"Unsupported delivery protocol '{option.Transcoding.Format?.Protocol ?? "unknown"}'."),
            };

            if (bytes is null)
            {
                // Fragmented MP4 with no muxer installed. Step down the ladder
                // rather than failing outright, but say so at Warning level: the
                // file that lands is a lower rung than the one ranked first, and
                // quietly handing over worse audio is exactly what this tool
                // exists not to do.
                LogSteppedDownForMuxer(logger, option.Rung.Preset, option.Rung.Kbps);
                attempts.Add($"{option.Rung.Preset} needs a muxer");

                continue;
            }

            return new DownloadResult
            {
                FilePath = destination,
                Source = DownloadSource.Transcoding,
                Bytes = bytes.Value,
                Rung = option.Rung,
                IsPreview = option.Transcoding.Snipped,
                FellBackFromOriginal = fellBackFromOriginal,
            };
        }

        throw new ScdlException(
            $"No rung could be downloaded. Tried: {string.Join("; ", attempts)}. "
            + "If every rung needs a muxer, install ffmpeg: winget install Gyan.FFmpeg");
    }

    /// <summary>
    /// The rungs to try, best first. Pinning <c>--format</c> yields exactly one,
    /// so an explicit choice is never silently downgraded.
    /// </summary>
    /// <exception cref="ScdlException">
    /// The track offers no playable stream, or the requested preset is not among
    /// the ones it does offer.
    /// </exception>
    private static IReadOnlyList<StreamOption> SelectCandidates(Track track, string? preset)
    {
        var ranked = TrackStreams.Rank(track);

        if (ranked.Count is 0)
        {
            throw new ScdlException("SoundCloud offered no playable stream for this track.");
        }

        if (preset is not { Length: > 0 })
        {
            return ranked;
        }

        if (TrackStreams.TryFindPreset(ranked, preset, out var match))
        {
            return [match];
        }

        var available = string.Join(", ", ranked.Select(option => option.Rung.Preset));

        throw new ScdlException($"Preset '{preset}' is not offered for this track. Available: {available}");
    }

    private async Task<long> DownloadProgressiveAsync(
        Uri streamUri,
        string destination,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await http
                                   .GetAsync(streamUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                   .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await WriteResponseAsync(response, destination, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches an HLS playlist and reassembles it. Plain segment playlists are
    /// concatenated here in managed code; only fragmented MP4 is handed to a
    /// muxer, which is what keeps ffmpeg an optional dependency.
    /// </summary>
    /// <returns>
    /// Bytes written, or null when the playlist is fragmented MP4 and no muxer
    /// is installed - a recoverable miss that the caller answers by trying the
    /// next rung down, unlike the failures below.
    /// </returns>
    /// <exception cref="ScdlException">
    /// The response was a master playlist rather than a media playlist, the
    /// playlist is encrypted, or it listed no segments at all. None of the three
    /// is recoverable here, and each says something different about why.
    /// </exception>
    private async Task<long?> TryDownloadHlsAsync(
        Uri playlistUri,
        string destination,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var content = await http.GetStringAsync(playlistUri, cancellationToken).ConfigureAwait(false);
        var playlist = HlsPlaylist.Parse(content, playlistUri);

        if (playlist.IsMasterPlaylist)
        {
            throw new ScdlException("SoundCloud returned a master playlist where a media playlist was expected.");
        }

        if (playlist.IsEncrypted)
        {
            throw new ScdlException("This HLS playlist is encrypted, which scdl does not handle.");
        }

        if (playlist.RequiresMuxer)
        {
            if (!muxer.IsAvailable)
            {
                return null;
            }

            LogFragmentedMp4(logger, playlist.Segments.Count);

            var partialPath = destination + PartialSuffix;
            await muxer.MuxAsync(playlistUri, partialPath, cancellationToken).ConfigureAwait(false);

            File.Move(partialPath, destination, overwrite: true);

            return new FileInfo(destination).Length;
        }

        if (playlist.Segments.Count is 0)
        {
            throw new ScdlException("The HLS playlist listed no segments.");
        }

        return await ConcatenateSegmentsAsync(playlist.Segments, destination, progress, cancellationToken)
                     .ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads segments in bounded parallel windows but writes them strictly
    /// in playlist order, because concatenated audio is order sensitive.
    /// </summary>
    private async Task<long> ConcatenateSegmentsAsync(
        IReadOnlyList<Uri> segments,
        string destination,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var partialPath = destination + PartialSuffix;

        try
        {
            var written = await FetchSegmentsAsync(segments, partialPath, progress, cancellationToken)
                                .ConfigureAwait(false);

            // The sink is disposed with the callee's scope, so the handle is
            // already closed here. Moving a file Windows still has open fails.
            File.Move(partialPath, destination, overwrite: true);

            return written;
        }
        catch
        {
            TryDelete(partialPath);

            throw;
        }
    }

    private async Task<long> FetchSegmentsAsync(
        IReadOnlyList<Uri> segments,
        string partialPath,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var sink = CreateSink(partialPath);

        long written = 0;

        foreach (var window in segments.Chunk(_options.MaxParallelSegments))
        {
            var fetches = new Task<byte[]>[window.Length];

            for (var i = 0; i < window.Length; i++)
            {
                fetches[i] = http.GetByteArrayAsync(window[i], cancellationToken);
            }

            var buffers = await Task.WhenAll(fetches).ConfigureAwait(false);

            foreach (var buffer in buffers)
            {
                await sink.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

                written += buffer.Length;

                // The total is genuinely unknown until the last segment lands,
                // so HLS reports bytes without a denominator.
                progress?.Report(new TransferProgress(written, null));
            }
        }

        return written;
    }

    /// <summary>
    /// Streams to a .part file and moves it into place on success, so an
    /// interrupted run never leaves a truncated file that looks complete.
    /// </summary>
    private static async Task<long> WriteResponseAsync(
        HttpResponseMessage response,
        string destination,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var partialPath = destination + PartialSuffix;

        try
        {
            var written = await CopyToPartialAsync(response, partialPath, progress, cancellationToken)
                                .ConfigureAwait(false);

            // Both streams are disposed with the callee's scope, so the handles
            // are already closed here. Moving a file Windows still has open fails.
            File.Move(partialPath, destination, overwrite: true);

            return written;
        }
        catch
        {
            TryDelete(partialPath);

            throw;
        }
    }

    private static async Task<long> CopyToPartialAsync(
        HttpResponseMessage response,
        string partialPath,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content
                                               .ReadAsStreamAsync(cancellationToken)
                                               .ConfigureAwait(false);

        await using var sink = CreateSink(partialPath);

        var buffer = new byte[CopyBufferSize];
        long written = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read is 0)
            {
                break;
            }

            await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            written += read;
            progress?.Report(new TransferProgress(written, total));
        }

        return written;
    }

    private static FileStream CreateSink(string path)
        => new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static string ResolveDestination(DownloadRequest request, string stem, string extension)
    {
        var path = Path.Combine(request.OutputDirectory, stem + extension);

        return request.Overwrite ? path : FileNaming.Deduplicate(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup; the original failure is the one worth surfacing.
        }
    }
}
