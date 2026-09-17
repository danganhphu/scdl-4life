using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scdl.Core.Downloading;
using Scdl.Core.SoundCloud;
using Scdl.Core.Tagging;

namespace Scdl.Core;

public static class ScdlServiceCollectionExtensions
{
    /// <summary>Registers everything needed to resolve, download and tag SoundCloud tracks.</summary>
    public static IServiceCollection AddScdl(this IServiceCollection services,
                                             Action<SoundCloudOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<SoundCloudOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(
                options => options.MaxParallelSegments
                               is >= SoundCloudOptions.MinParallelSegments and
                                  <= SoundCloudOptions.MaxAllowedParallelSegments,
                $"MaxParallelSegments must be between {SoundCloudOptions.MinParallelSegments} " +
                $"and {SoundCloudOptions.MaxAllowedParallelSegments}.")
            .Validate(
                options => options.ClientIdCacheLifetime > TimeSpan.Zero,
                "ClientIdCacheLifetime must be positive.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                "UserAgent must not be empty.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMediaMuxer, FfmpegMuxer>();

        // Metadata calls are small and idempotent, so they get retries.
        AddApiClient<IClientIdProvider, ClientIdProvider>(services);
        AddApiClient<ISoundCloudClient, SoundCloudClient>(services);
        AddApiClient<IMediaTagger, AtlMediaTagger>(services);

        // Transfers deliberately skip the standard resilience handler: its
        // default total-request timeout would abort a long download partway
        // through, and a retry would restart a multi-megabyte body from zero.
        AddTransferClient<ITrackDownloader, TrackDownloader>(services);

        return services;
    }

    private static void AddApiClient<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        // Configured through the builder rather than an AddHttpClient overload.
        // The two-generic AddHttpClient set also contains
        // Func<HttpClient, TImplementation>, a typed-client factory, so a method
        // group there reads as ambiguous even though Roslyn binds the Action.
        // ConfigureHttpClient has no such twin.
        services.AddHttpClient<TInterface, TImplementation>()
                .ConfigureHttpClient(ConfigureCommon)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                .AddStandardResilienceHandler();
    }

    private static void AddTransferClient<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>()
                .ConfigureHttpClient(ConfigureTransfer)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler);
    }

    private static void ConfigureTransfer(HttpClient client)
    {
        ConfigureCommon(client);

        // A transfer is allowed to take as long as the file needs; the default
        // 100 seconds would cut off any sizeable download.
        client.Timeout = TimeSpan.FromMinutes(30);
    }

    private static void ConfigureCommon(HttpClient client)
        => client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/130.0.0.0 Safari/537.36");

    private static HttpMessageHandler CreateHandler()
        => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 8,
        };
}
