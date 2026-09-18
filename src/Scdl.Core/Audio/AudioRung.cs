namespace Scdl.Core.Audio;

/// <summary>
/// One rung of SoundCloud's transcoding ladder, carrying the bitrate the CDN
/// genuinely serves rather than the one a downloader site advertises.
/// </summary>
/// <param name="Preset">SoundCloud's own preset identifier, for example <c>aac_256k</c>.</param>
/// <param name="Kbps">Real bitrate, or 0 when the preset is unrecognised.</param>
/// <param name="Codec">Codec the rung decodes to.</param>
/// <param name="FileExtension">Container extension that holds this codec without transcoding.</param>
/// <param name="RequiresGoPlus">True when only a Go+ token unlocks the rung.</param>
public readonly record struct AudioRung(string Preset,
                                        int Kbps,
                                        AudioCodec Codec,
                                        string FileExtension,
                                        bool RequiresGoPlus)
{
    /// <summary>True when SoundCloud named a preset this build has never heard of.</summary>
    public bool IsUnknown => Kbps is 0;

    public override string ToString()
        => IsUnknown ? $"{Preset} (unrecognised)" : $"{Preset} ({Kbps} kbps {Codec})";
}
