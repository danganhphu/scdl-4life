using Scdl.Core.Audio;

namespace Scdl.Core.Tests;

public sealed class AudioFileExtensionsTests
{
    [Test]
    [Arguments("Some Mix.mp3", "Some Mix")]
    [Arguments("Some Mix.MP3", "Some Mix")]
    [Arguments("Some Mix.m4a", "Some Mix")]
    [Arguments("Some Mix.flac", "Some Mix")]
    [Arguments("Some Mix .mp3", "Some Mix")]
    public async Task StripFrom_drops_a_trailing_audio_extension(string raw, string expected)
    {
        await Assert.That(AudioFileExtensions.StripFrom(raw)).IsEqualTo(expected);
    }

    /// <summary>
    /// Track titles are full of dots that are not extensions. Stripping by
    /// "everything after the last dot" would turn "Vol. 2" into "Vol" and
    /// "Remix feat. Someone" into "Remix feat".
    /// </summary>
    [Test]
    [Arguments("Mixtape Vol. 2")]
    [Arguments("Remix feat. Someone")]
    [Arguments("No dots at all")]
    [Arguments("ends with a dot.")]
    [Arguments("Track.wtf")]
    public async Task StripFrom_leaves_a_dot_that_is_not_an_audio_extension(string raw)
    {
        await Assert.That(AudioFileExtensions.StripFrom(raw)).IsEqualTo(raw);
    }

    /// <summary>
    /// The set is what tells a title from a file name, so an entry missing from
    /// it means a real title silently keeps its extension.
    /// </summary>
    [Test]
    public async Task All_holds_every_extension_the_ladder_can_produce()
    {
        foreach (var rung in TranscodingCatalog.AllRungs)
        {
            await Assert.That(AudioFileExtensions.All.Contains(rung.FileExtension)).IsTrue();
        }
    }

    /// <summary>The placeholder for an unidentifiable container is not audio and must not be in the set.</summary>
    [Test]
    public async Task All_excludes_the_unidentified_placeholder()
    {
        await Assert.That(AudioFileExtensions.All.Contains(AudioFileExtensions.Unidentified)).IsFalse();
    }
}
