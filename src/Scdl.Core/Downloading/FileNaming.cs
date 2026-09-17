using System.Buffers;
using System.Text;
using Scdl.Core.SoundCloud;

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

        return cleaned.Length == 0 ? "untitled" : cleaned;
    }

    public static string BuildStem(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        return Sanitize($"{track.DisplayArtist} - {track.DisplayTitle}");
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
