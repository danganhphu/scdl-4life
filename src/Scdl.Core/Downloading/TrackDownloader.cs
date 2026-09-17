using Scdl.Core.Audio;
using Scdl.Core.SoundCloud;

namespace Scdl.Core.Downloading;

public sealed partial class TrackDownloader : ITrackDownloader
{
    private const int CopyBufferSize = 128 * 1024;

    private readonly HttpClient _http;
    private readonly ISoundCloudClient _soundCloud;
    private readonly IMediaMuxer _muxer;
    private readonly SoundCloudOptions _options;
    private readonly ILogger<TrackDownloader> _logger;

    public TrackDownloader(HttpClient http,
                           ISoundCloudClient soundCloud,
                           IMediaMuxer muxer,
                           IOptions<SoundCloudOptions> options,
                           ILogger<TrackDownloader> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _soundCloud = soundCloud;
        _muxer = muxer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request,
                                                    IProgress<TransferProgress>? progress,
                                                    CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Directory.CreateDirectory(request.OutputDirectory);

        var stem = FileNaming.BuildStem(request.Track);

        if (request.Prefer == SourcePreference.Original)
        {
            var original = await TryDownloadOriginalAsync(request, stem, progress, cancellationToken)
                               .ConfigureAwait(false);

            if (original is not null)
            {
                return original;
            }

            LogNoOriginal(request.Track.Id);
        }

        return await DownloadTranscodingAsync(
                       request,
                       stem,
                       fellBackFromOriginal: request.Prefer == SourcePreference.Original,
                       progress,
                       cancellationToken)
                   .ConfigureAwait(false);
    }

    /// <summary>
    /// The only route to genuinely lossless audio, and only when the uploader
    /// enabled downloads. Returns null when that route is closed.
    /// </summary>
    private async Task<DownloadResult?> TryDownloadOriginalAsync(DownloadRequest request,
                                                                 string stem,
                                                                 IProgress<TransferProgress>? progress,
                                                                 CancellationToken cancellationToken)
    {
        var originalUri = await _soundCloud
                                .TryGetOriginalUriAsync(request.Track, cancellationToken)
                                .ConfigureAwait(false);

        if (originalUri is null)
        {
            return null;
        }

        using var response = await _http
                                   .GetAsync(originalUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                   .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // The extension is only knowable from the response, because the original
        // is whatever the uploader submitted: wav, flac, aiff or mp3.
        var destination = ResolveDestination(request, stem, FileNaming.ExtensionFor(response));
        var bytes = await WriteResponseAsync(response, destination, progress, cancellationToken)
                        .ConfigureAwait(false);

        return new DownloadResult { FilePath = destination, Source = DownloadSource.OriginalMaster, Bytes = bytes, };
    }

    private async Task<DownloadResult> DownloadTranscodingAsync(DownloadRequest request,
                                                                string stem,
                                                                bool fellBackFromOriginal,
                                                                IProgress<TransferProgress>? progress,
                                                                CancellationToken cancellationToken)
    {
        var option = SelectOption(request.Track, request.Preset);
        var streamUri = await _soundCloud
                              .GetStreamUriAsync(request.Track, option.Transcoding, cancellationToken)
                              .ConfigureAwait(false);

        var destination = ResolveDestination(request, stem, option.Rung.FileExtension);

        var bytes = option.Protocol switch
        {
            DeliveryProtocol.Progressive =>
                await DownloadProgressiveAsync(streamUri, destination, progress, cancellationToken)
                    .ConfigureAwait(false),

            DeliveryProtocol.Hls =>
                await DownloadHlsAsync(streamUri, destination, progress, cancellationToken)
                    .ConfigureAwait(false),

            _ => throw new ScdlException(
                     $"Unsupported delivery protocol '{option.Transcoding.Format?.Protocol ?? "unknown"}'."),
        };

        return new DownloadResult
        {
            FilePath = destination,
            Source = DownloadSource.Transcoding,
            Bytes = bytes,
            Rung = option.Rung,
            IsPreview = option.Transcoding.Snipped,
            FellBackFromOriginal = fellBackFromOriginal,
        };
    }

    private static StreamOption SelectOption(Track track, string? preset)
    {
        var ranked = TrackStreams.Rank(track);

        if (ranked.Count == 0)
        {
            throw new ScdlException("SoundCloud offered no playable stream for this track.");
        }

        if (preset is not { Length: > 0 })
        {
            return ranked[0];
        }

        if (TrackStreams.TryFindPreset(ranked, preset, out var match))
        {
            return match;
        }

        var available = string.Join(", ", ranked.Select(option => option.Rung.Preset));

        throw new ScdlException($"Preset '{preset}' is not offered for this track. Available: {available}");
    }

    private async Task<long> DownloadProgressiveAsync(Uri streamUri,
                                                      string destination,
                                                      IProgress<TransferProgress>? progress,
                                                      CancellationToken cancellationToken)
    {
        using var response = await _http
                                   .GetAsync(streamUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                   .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await WriteResponseAsync(response, destination, progress, cancellationToken)
                   .ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches an HLS playlist and reassembles it. Plain segment playlists are
    /// concatenated here in managed code; only fragmented MP4 is handed to a
    /// muxer, which is what keeps ffmpeg an optional dependency.
    /// </summary>
    /// <exception cref="ScdlException">
    /// The response was a master playlist rather than a media playlist, the
    /// playlist is encrypted, or it listed no segments at all. None of the three
    /// is recoverable here, and each says something different about why.
    /// </exception>
    private async Task<long> DownloadHlsAsync(Uri playlistUri,
                                              string destination,
                                              IProgress<TransferProgress>? progress,
                                              CancellationToken cancellationToken)
    {
        var content = await _http.GetStringAsync(playlistUri, cancellationToken).ConfigureAwait(false);
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
            LogFragmentedMp4(playlist.Segments.Count);

            var partialPath = destination + ".part";
            await _muxer.MuxAsync(playlistUri, partialPath, cancellationToken).ConfigureAwait(false);

            File.Move(partialPath, destination, overwrite: true);

            return new FileInfo(destination).Length;
        }

        if (playlist.Segments.Count == 0)
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
    private async Task<long> ConcatenateSegmentsAsync(IReadOnlyList<Uri> segments,
                                                      string destination,
                                                      IProgress<TransferProgress>? progress,
                                                      CancellationToken cancellationToken)
    {
        var partialPath = destination + ".part";

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

    private async Task<long> FetchSegmentsAsync(IReadOnlyList<Uri> segments,
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
                fetches[i] = _http.GetByteArrayAsync(window[i], cancellationToken);
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

    private static FileStream CreateSink(string path)
        => new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>
    /// Streams to a .part file and moves it into place on success, so an
    /// interrupted run never leaves a truncated file that looks complete.
    /// </summary>
    private static async Task<long> WriteResponseAsync(HttpResponseMessage response,
                                                       string destination,
                                                       IProgress<TransferProgress>? progress,
                                                       CancellationToken cancellationToken)
    {
        var partialPath = destination + ".part";

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

    private static async Task<long> CopyToPartialAsync(HttpResponseMessage response,
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

            if (read == 0)
            {
                break;
            }

            await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            written += read;
            progress?.Report(new TransferProgress(written, total));
        }

        return written;
    }

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

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Track {TrackId} offers no original master; falling back to a transcoding.")]
    private partial void LogNoOriginal(long trackId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Playlist uses fragmented MP4 across {SegmentCount} segment(s); handing it to the muxer.")]
    private partial void LogFragmentedMp4(int segmentCount);
}
