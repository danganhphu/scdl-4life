namespace Scdl.Core.Tests.Fakes;

/// <summary>
/// A scratch directory for the one part of the downloader that cannot be faked
/// away: it writes files.
/// </summary>
/// <remarks>
/// A real directory rather than an abstraction over the filesystem. The .part
/// handling, the move into place and the cleanup after a failure are exactly the
/// behaviour worth testing, and an IFileSystem seam would test the seam instead.
/// </remarks>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "scdl-tests", Guid.CreateVersion7().ToString("n"));

        Directory.CreateDirectory(FullPath);
    }

    public string FullPath { get; }

    /// <summary>Every file left behind, so a test can assert that nothing partial survived.</summary>
    public IReadOnlyList<string> Files
        => Directory.GetFiles(FullPath, "*", SearchOption.AllDirectories);

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover scratch directory is not worth failing a green test over.
        }
    }
}
