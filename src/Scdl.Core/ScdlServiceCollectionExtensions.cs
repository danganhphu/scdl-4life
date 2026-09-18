using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scdl.Core.Downloading;
using Scdl.Core.Downloading.Hls;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.ClientId;
using Scdl.Core.Tagging;

namespace Scdl.Core;

/// <summary>Registers everything needed to resolve, download and tag SoundCloud tracks.</summary>
public static class ScdlServiceCollectionExtensions
{
    private const string UserAgentHeader = "User-Agent";

    /// <summary>A transfer runs as long as the file needs; the 100 second default would cut it off.</summary>
    private static readonly TimeSpan TransferTimeout = TimeSpan.FromMinutes(30);

    public static IServiceCollection AddScdl(
        this IServiceCollection services,
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
                    is >= SoundCloudOptions.MinParallelSegments
                    and <= SoundCloudOptions.MaxAllowedParallelSegments,
                $"{nameof(SoundCloudOptions.MaxParallelSegments)} must be between "
                + $"{SoundCloudOptions.MinParallelSegments} and {SoundCloudOptions.MaxAllowedParallelSegments}.")
            .Validate(
                options => options.ClientIdCacheLifetime > TimeSpan.Zero,
                $"{nameof(SoundCloudOptions.ClientIdCacheLifetime)} must be positive.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                $"{nameof(SoundCloudOptions.UserAgent)} must not be empty.")
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
                .ConfigureHttpClient(ApplyUserAgent)
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
                .ConfigureHttpClient(ApplyTransferSettings)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler);
    }

    /// <summary>
    /// Reads the agent string from options rather than restating it, so
    /// <see cref="SoundCloudOptions.UserAgent"/> is configuration that actually
    /// takes effect. The CDN is noticeably less cooperative with a default .NET
    /// agent, which is why the default looks like a browser.
    /// </summary>
    private static void ApplyUserAgent(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<SoundCloudOptions>>().Value;

        client.DefaultRequestHeaders.TryAddWithoutValidation(UserAgentHeader, options.UserAgent);
    }

    private static void ApplyTransferSettings(IServiceProvider provider, HttpClient client)
    {
        ApplyUserAgent(provider, client);

        client.Timeout = TransferTimeout;
    }

    private static HttpMessageHandler CreateHandler()
        => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 8,
        };
}
