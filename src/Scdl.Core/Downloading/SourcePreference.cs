namespace Scdl.Core.Downloading;

/// <summary>Which of the two routes to a file the caller would rather have.</summary>
public enum SourcePreference
{
    /// <summary>Take the uploader's original master when offered, best transcoding otherwise.</summary>
    Original = 0,

    /// <summary>Always take a transcoding, even when an original is available.</summary>
    Stream = 1,
}
