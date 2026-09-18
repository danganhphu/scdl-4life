using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scdl.Core.SoundCloud;

namespace Scdl.Core.Tests;

/// <summary>
/// Proves the options validator the source generator emits is actually wired up
/// and running. Without a test the attributes compile happily and validate
/// nothing, which is the exact failure this repo has already hit once with
/// doc-comment analyzers.
/// </summary>
public sealed class SoundCloudOptionsValidationTests
{
    private static SoundCloudOptions Resolve(Action<SoundCloudOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScdl(configure);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<SoundCloudOptions>>().Value;
    }

    [Test]
    public async Task Defaults_are_valid()
    {
        var options = Resolve(_ => { });

        await Assert.That(options.MaxParallelSegments).IsEqualTo(4);
        await Assert.That(options.ClientIdCacheLifetime).IsEqualTo(TimeSpan.FromDays(7));
    }

    [Test, Arguments(0), Arguments(17)]
    public async Task MaxParallelSegments_outside_the_range_is_rejected(int value)
    {
        var exception =
            Assert.Throws<OptionsValidationException>(() => Resolve(options => options.MaxParallelSegments = value));

        await Assert.That(exception).IsNotNull();
        await Assert
              .That(
                  exception!.Failures.Any(failure => failure.Contains(
                      nameof(SoundCloudOptions.MaxParallelSegments),
                      StringComparison.Ordinal)))
              .IsTrue();
    }

    [Test]
    public async Task An_empty_user_agent_is_rejected()
    {
        var exception =
            Assert.Throws<OptionsValidationException>(() => Resolve(options => options.UserAgent = string.Empty));

        await Assert.That(exception).IsNotNull();
    }

    [Test, Arguments(0), Arguments(366)]
    public async Task ClientIdCacheDays_outside_the_range_is_rejected(int value)
    {
        var exception =
            Assert.Throws<OptionsValidationException>(() => Resolve(options => options.ClientIdCacheDays = value));

        await Assert.That(exception).IsNotNull();
    }
}
