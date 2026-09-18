using System.Collections.Frozen;

namespace Scdl.Core.Audio;

/// <summary>
/// Every file extension this tool writes or recognises.
/// </summary>
/// <remarks>
/// These were spelled out at three sites: the ladder in
/// <see cref="TranscodingCatalog"/>, the title cleanup in
/// <c>FileNaming.StripAudioExtension</c>, and the Content-Type fallback in
/// <c>FileNaming.ExtensionFor</c>. Three literals for one fact is three chances
/// to type <c>.mp4</c> where <c>.m4a</c> was meant and hand the user a file
/// nothing will open.
/// </remarks>
public static class AudioFileExtensions
{
    /// <summary>Containers the ladder itself produces.</summary>
    public const string Mp3 = ".mp3";

    /// <inheritdoc cref="Mp3"/>
    public const string M4a = ".m4a";

    /// <inheritdoc cref="Mp3"/>
    public const string Ogg = ".ogg";

    /// <summary>Containers only an uploader's original master arrives in.</summary>
    public const string Wav = ".wav";

    /// <inheritdoc cref="Wav"/>
    public const string Flac = ".flac";

    /// <inheritdoc cref="Wav"/>
    public const string Aiff = ".aiff";

    /// <inheritdoc cref="Wav"/>
    public const string Aif = ".aif";

    /// <inheritdoc cref="Wav"/>
    public const string Mp4 = ".mp4";

    /// <inheritdoc cref="Wav"/>
    public const string Aac = ".aac";

    /// <inheritdoc cref="Wav"/>
    public const string Opus = ".opus";

    /// <inheritdoc cref="Wav"/>
    public const string Wma = ".wma";

    /// <summary>
    /// What an unidentifiable container gets. Deliberately not audio, and
    /// deliberately not a guess: a wrong extension is worse than an honest one.
    /// </summary>
    public const string Unidentified = ".bin";

    /// <summary>
    /// Every audio extension above, compared case insensitively. Used to spot a
    /// title that is really a file name, so "Some Mix.mp3" does not become
    /// "Some Mix.mp3.mp3".
    /// </summary>
    public static FrozenSet<string> All { get; } = new[]
    {
        Mp3,
        M4a,
        Mp4,
        Aac,
        Wav,
        Flac,
        Ogg,
        Opus,
        Aiff,
        Aif,
        Wma,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Drops a trailing audio extension from a name.
    /// </summary>
    /// <remarks>
    /// Uploaders routinely upload "Some Mix.mp3" and SoundCloud keeps the file
    /// name as the track title. Left alone it produces "... .mp3.mp3" on disk
    /// and a track called "... .mp3" in every player. This lives here, next to
    /// the extensions themselves, so the one caller that remembered to strip and
    /// the two that forgot cannot diverge again.
    /// </remarks>
    public static string StripFrom(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var extension = Path.GetExtension(name);

        return extension.Length > 1 && All.Contains(extension)
                   ? name[..^extension.Length].TrimEnd()
                   : name;
    }
}
