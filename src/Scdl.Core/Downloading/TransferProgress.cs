namespace Scdl.Core.Downloading;

/// <summary>
/// How far a transfer has got, in the unit the step can actually measure.
/// </summary>
/// <remarks>
/// Two shapes, because the halves of a download do not measure the same thing.
/// Anything that writes as it goes counts bytes. A mux counts stream time: the
/// MP4 muxer holds every sample until it writes the trailer, so the file on disk
/// stays at its header length for the whole run and there are no bytes to count.
/// That was measured against ffmpeg, not assumed - <c>total_size</c> sat at 44
/// while <c>out_time_us</c> climbed.
/// </remarks>
public readonly record struct TransferProgress
{
    private TransferProgress(long bytesTransferred,
                             long? totalBytes,
                             TimeSpan streamTime,
                             TimeSpan streamDuration)
    {
        BytesTransferred = bytesTransferred;
        TotalBytes = totalBytes;
        StreamTime = streamTime;
        StreamDuration = streamDuration;
    }

    /// <summary>Bytes written so far.</summary>
    /// <param name="transferred">What has landed.</param>
    /// <param name="total">
    /// The expected size, or null when the server declined to send a length,
    /// which is normal for HLS where the total is only known once every segment
    /// has been fetched.
    /// </param>
    public static TransferProgress FromBytes(long transferred, long? total)
        => new(transferred, total, TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>How far into the stream a mux has reached.</summary>
    /// <param name="reached">The position ffmpeg reports.</param>
    /// <param name="duration">How long the assembled audio runs.</param>
    public static TransferProgress FromStreamTime(TimeSpan reached, TimeSpan duration)
        => new(0, null, reached, duration);

    public long BytesTransferred { get; }

    public long? TotalBytes { get; }

    public TimeSpan StreamTime { get; }

    public TimeSpan StreamDuration { get; }

    /// <summary>True when this tick counts seconds, so there are no bytes to show.</summary>
    public bool IsStreamTime => StreamDuration > TimeSpan.Zero;

    /// <summary>How much of the work is done, or null when nothing measures the whole.</summary>
    public double? Fraction
    {
        get
        {
            if (IsStreamTime)
            {
                return Math.Clamp(StreamTime / StreamDuration, 0d, 1d);
            }

            return TotalBytes is > 0 ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d) : null;
        }
    }
}
