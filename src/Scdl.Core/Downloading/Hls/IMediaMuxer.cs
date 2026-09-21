namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// Reassembles media that cannot simply be concatenated. Only fragmented MP4
/// HLS needs this; everything else is handled in managed code, which is what
/// keeps the external muxer an optional dependency.
/// </summary>
internal interface IMediaMuxer
{
    bool IsAvailable { get; }

    /// <summary>Reassembles a playlist into a single file.</summary>
    /// <param name="playlistUri">The media playlist to read.</param>
    /// <param name="outputPath">Where to write, typically a temporary name.</param>
    /// <param name="containerExtension">
    /// The extension of the file that will finally exist, such as <c>.m4a</c>.
    /// Passed separately because <paramref name="outputPath"/> is a temporary
    /// name whose own extension says nothing about the container, and a muxer
    /// that guesses from the path gets it wrong.
    /// </param>
    /// <param name="duration">
    /// How long the assembled audio runs, which is the only thing a mux can
    /// measure itself against - see <see cref="TransferProgress"/>.
    /// </param>
    /// <param name="progress">
    /// Receives the position reached in the stream. A mux is the whole transfer
    /// for the tracks that need one, so without this the progress bar has
    /// nothing to report for the entire download.
    /// </param>
    /// <param name="cancellationToken">Cancels the muxing process.</param>
    Task MuxAsync(Uri playlistUri,
                  string outputPath,
                  string containerExtension,
                  TimeSpan duration,
                  IProgress<TransferProgress>? progress,
                  CancellationToken cancellationToken);
}
