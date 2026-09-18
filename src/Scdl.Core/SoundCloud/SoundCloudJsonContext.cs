using Scdl.Core.SoundCloud.ClientId;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// Source generated contracts. api-v2 is an undocumented internal API that adds
/// fields without notice, so unmapped members are ignored rather than fatal.
/// Source generation is what keeps the whole client trim and AOT safe.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(ResolvedKind))]
[JsonSerializable(typeof(Track))]
[JsonSerializable(typeof(Track[]))]
[JsonSerializable(typeof(Playlist))]
[JsonSerializable(typeof(StreamLocation))]
[JsonSerializable(typeof(OriginalDownload))]
[JsonSerializable(typeof(CachedClientId))]
internal sealed partial class SoundCloudJsonContext : JsonSerializerContext;
