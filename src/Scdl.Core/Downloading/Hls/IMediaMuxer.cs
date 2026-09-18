namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// Reassembles media that cannot simply be concatenated. Only fragmented MP4
/// HLS needs this; everything else is handled in managed code, which is what
/// keeps the external muxer an optional dependency.
/// </summary>
internal interface IMediaMuxer
{
    bool IsAvailable { get; }

    Task MuxAsync(Uri playlistUri, string outputPath, CancellationToken cancellationToken);
}
