namespace Scdl.Core.Tests.Fakes;

/// <summary>Deterministic clock, so cache expiry can be tested without waiting a week.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow()
        => Now;
}
