using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.Downloading;

public static class FileNaming
{
    /// <summary>
    /// Windows caps a path component at 255 characters, and taggers get unhappy
    /// well before that, so the stem is trimmed with room left for an extension
    /// and a deduplication suffix.
    /// </summary>
    private const int MaxStemLength = 150;

    private static readonly SearchValues<char> InvalidCharacters =
        SearchValues.Create(new string(Path.GetInvalidFileNameChars()) + ":*?\"<>|");

    private static readonly FrozenSet<string> AudioExtensions = new[]
    {
        ".mp3", ".m4a", ".mp4", ".aac", ".wav", ".flac", ".ogg", ".opus", ".aiff", ".aif", ".wma",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Collapses anything unsafe into single spaces and trims to a sane length.</summary>
    public static string Sanitize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var builder = new StringBuilder(raw.Length);
        var lastWasSpace = false;

        foreach (var character in raw)
        {
            var replaced = InvalidCharacters.Contains(character) || char.IsControl(character)
                               ? ' '
                               : character;

            if (replaced == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(replaced);
        }

        var cleaned = builder.ToString().Trim().TrimEnd('.');

        if (cleaned.Length > MaxStemLength)
        {
            cleaned = cleaned[..MaxStemLength].TrimEnd();
        }

        return cleaned.Length is 0 ? "untitled" : cleaned;
    }

    public static string BuildStem(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        return Sanitize($"{track.DisplayArtist} - {StripAudioExtension(track.DisplayTitle)}");
    }

    /// <summary>
    /// Drops a trailing audio extension from a track title. Uploaders routinely
    /// upload "Some Mix.mp3" and SoundCloud keeps the file name as the title, so
    /// appending the container extension would otherwise produce "....mp3.mp3".
    /// </summary>
    internal static string StripAudioExtension(string title)
    {
        var extension = Path.GetExtension(title);

        return extension.Length > 1 && AudioExtensions.Contains(extension)
            ? title[..^extension.Length].TrimEnd()
            : title;
    }

    /// <summary>
    /// Works out the extension of an original master, which can be anything the
    /// uploader submitted. Content-Disposition is authoritative when present.
    /// </summary>
    public static string ExtensionFor(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var disposition = response.Content.Headers.ContentDisposition;
        var name = disposition?.FileNameStar ?? disposition?.FileName;

        if (name is { Length: > 0 } && Path.GetExtension(name.Trim('"')) is { Length: > 1 } fromDisposition)
        {
            return fromDisposition.ToLowerInvariant();
        }

        return response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/aiff" or "audio/x-aiff" => ".aiff",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/mp4" or "audio/x-m4a" => ".m4a",
            "audio/ogg" => ".ogg",
            _ => ".mp3",
        };
    }

    /// <summary>Appends " (2)", " (3)" and so on rather than clobbering an existing file.</summary>
    public static string Deduplicate(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);

        for (var n = 2;; n++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({n}){extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
