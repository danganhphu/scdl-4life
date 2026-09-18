namespace Scdl.Core.Audio;

/// <summary>
/// How a transcoding is delivered. Progressive is a single file; HLS is a
/// playlist of segments that may or may not need a real muxer to reassemble.
/// </summary>
public enum DeliveryProtocol
{
    Unknown = 0,
    Progressive = 1,
    Hls = 2,
}
