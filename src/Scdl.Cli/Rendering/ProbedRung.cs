using Scdl.Core.SoundCloud;

namespace Scdl.Cli.Rendering;

/// <summary>A rung the ladder advertises, paired with what came back when it was asked for.</summary>
/// <param name="Option">The advertised rung.</param>
/// <param name="UnservedReason">
/// Why SoundCloud would not hand over a stream for it, or null when it would.
/// </param>
internal sealed record ProbedRung(StreamOption Option, string? UnservedReason)
{
    public bool IsServed => UnservedReason is null;
}
