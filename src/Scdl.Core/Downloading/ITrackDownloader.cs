namespace Scdl.Core.Downloading;

/// <summary>Fetches one track to disk, reporting bytes as they land.</summary>
public interface ITrackDownloader
{
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken);
}
