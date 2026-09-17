namespace Scdl.Core.Downloading;

/// <summary>
/// A transfer tick. <see cref="TotalBytes"/> is null when the server declined to
/// send a length, which is normal for HLS where the total is only known once
/// every segment has been fetched.
/// </summary>
public readonly record struct TransferProgress(long BytesTransferred, long? TotalBytes)
{
    public double? Fraction
        => TotalBytes is > 0 ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d) : null;
}
