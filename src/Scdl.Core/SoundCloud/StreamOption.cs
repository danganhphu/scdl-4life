using Scdl.Core.Audio;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.SoundCloud;

/// <summary>One playable way to get a track, paired with what it actually costs in quality.</summary>
public readonly record struct StreamOption(Transcoding Transcoding,
                                           AudioRung Rung,
                                           DeliveryProtocol Protocol);
