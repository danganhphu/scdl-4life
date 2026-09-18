namespace Scdl.Core.Tagging;

/// <summary>Where a track sat in the set it was downloaded from.</summary>
/// <remarks>
/// Without this a set arrives as N files with no album and no track number, and
/// every player sorts them by file name - which for a set is almost never the
/// running order the uploader chose.
/// </remarks>
/// <param name="Album">The set's title.</param>
/// <param name="Number">One-based position in the set.</param>
/// <param name="Total">How many tracks the set holds.</param>
public readonly record struct SetPosition(string Album, int Number, int Total);
