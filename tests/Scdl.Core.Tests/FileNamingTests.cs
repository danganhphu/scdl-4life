using Bogus;
using Scdl.Core.Downloading;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.Tests;

public sealed class FileNamingTests
{
    [Test]
    [Arguments("Artist / Title", "Artist Title")]
    [Arguments("a<b>c:d\"e|f?g*h", "a b c d e f g h")]
    [Arguments("   padded   ", "padded")]
    [Arguments("trailing dots...", "trailing dots")]
    [Arguments("", "untitled")]
    public async Task Sanitize_removes_characters_windows_rejects(string raw, string expected)
    {
        await Assert.That(FileNaming.Sanitize(raw)).IsEqualTo(expected);
    }

    [Test]
    public async Task Sanitize_collapses_runs_of_replaced_characters_into_one_space()
    {
        await Assert.That(FileNaming.Sanitize("a///b")).IsEqualTo("a b");
    }

    [Test]
    public async Task Sanitize_caps_the_stem_so_the_path_component_stays_legal()
    {
        var raw = new string('x', 400);

        var sanitized = FileNaming.Sanitize(raw);

        await Assert.That(sanitized.Length).IsLessThanOrEqualTo(150);
    }

    /// <summary>
    /// Bogus supplies the adversarial input here: real track titles are full of
    /// punctuation, and the only property that must hold for all of them is that
    /// the result is a legal, non-empty path component.
    /// </summary>
    [Test]
    public async Task Sanitize_always_produces_a_legal_path_component()
    {
        var faker = new Faker { Random = new(localSeed: 20260917) };
        var invalid = Path.GetInvalidFileNameChars();

        for (var i = 0; i < 200; i++)
        {
            var raw = faker.Lorem.Sentence() + new string(faker.PickRandom(invalid), 3) + faker.Lorem.Word();

            var sanitized = FileNaming.Sanitize(raw);

            await Assert.That(sanitized.Length).IsGreaterThan(0);
            await Assert.That(sanitized.IndexOfAny(invalid)).IsEqualTo(-1);
            await Assert.That(sanitized.EndsWith('.')).IsFalse();
        }
    }

    [Test]
    public async Task BuildStem_prefers_publisher_metadata_over_the_uploader_name()
    {
        var track = new Track
        {
            Id = 1,
            Title = "Song",
            User = new() { Username = "uploader-account" },
            PublisherMetadata = new() { Artist = "Real Artist" },
        };

        await Assert.That(FileNaming.BuildStem(track)).IsEqualTo("Real Artist - Song");
    }

    [Test]
    public async Task BuildStem_falls_back_to_the_uploader_then_to_a_placeholder()
    {
        var withUser = new Track { Id = 1, Title = "Song", User = new() { Username = "dj" } };
        var bare = new Track { Id = 42 };

        await Assert.That(FileNaming.BuildStem(withUser)).IsEqualTo("dj - Song");
        await Assert.That(FileNaming.BuildStem(bare)).IsEqualTo("Unknown Artist - track-42");
    }

    /// <summary>
    /// Uploaders leave the ".mp3" of the file they uploaded in the title, and
    /// SoundCloud keeps it. Appending the container extension on top would give
    /// "... .mp3.mp3". DisplayTitle drops it once, so the file name, the tag and
    /// the console heading all agree.
    /// </summary>
    [Test]
    public async Task BuildStem_drops_an_extension_the_uploader_left_in_the_title()
    {
        var track = new Track
        {
            Id = 1,
            Title = "Người Phản Bội x Lá Xa Lìa Cành - NSon Mix.mp3",
            User = new() { Username = "NSon Remix" },
        };

        await Assert.That(FileNaming.BuildStem(track))
                    .IsEqualTo("NSon Remix - Người Phản Bội x Lá Xa Lìa Cành - NSon Mix");
    }

    [Test]
    public async Task Deduplicate_returns_the_original_path_when_nothing_is_there()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scdl-{Guid.CreateVersion7():N}.mp3");

        await Assert.That(FileNaming.Deduplicate(path)).IsEqualTo(path);
    }

    [Test]
    public async Task Deduplicate_appends_a_counter_rather_than_clobbering()
    {
        var directory = Directory.CreateTempSubdirectory("scdl-tests");

        try
        {
            var path = Path.Combine(directory.FullName, "song.mp3");
            await File.WriteAllTextAsync(path, "occupied");

            var deduplicated = FileNaming.Deduplicate(path);

            await Assert.That(deduplicated).IsEqualTo(Path.Combine(directory.FullName, "song (2).mp3"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
