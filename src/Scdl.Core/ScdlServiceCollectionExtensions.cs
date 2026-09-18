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

    extension(IServiceCollection services)
    {
        public IServiceCollection AddScdl(Action<SoundCloudOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var optionsBuilder = services.AddOptions<SoundCloudOptions>().ValidateOnStart();

            if (configure is not null)
            {
                optionsBuilder.Configure(configure);
            }

            // The rules themselves are DataAnnotations on SoundCloudOptions, and
            // ValidateSoundCloudOptions is source generated from them. There is
            // deliberately no ValidateDataAnnotations() call: that one reflects,
            // and the generated validator exists precisely to avoid it.
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SoundCloudOptions>, ValidateSoundCloudOptions>());

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IMediaMuxer, FfmpegMuxer>();

            AddApiClient<IClientIdProvider, ClientIdProvider>(services);
            AddApiClient<ISoundCloudClient, SoundCloudClient>(services);
            AddApiClient<IMediaTagger, AtlMediaTagger>(services);

            AddTransferClient<ITrackDownloader, TrackDownloader>(services);

            return services;
        }
    }

    /// <summary>A typed client for the small, idempotent metadata calls, so it gets retries.</summary>
    /// <remarks>
    /// The agent string is applied through the builder rather than through an
    /// AddHttpClient overload. The two-generic AddHttpClient set also contains
    /// <c>Func&lt;HttpClient, TImplementation&gt;</c>, a typed-client factory, so a
    /// method group passed there reads as ambiguous even though Roslyn binds the
    /// Action. ConfigureHttpClient has no such twin.
    /// </remarks>
    private static void AddApiClient<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TImplementation>(IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
        => services.AddHttpClient<TInterface, TImplementation>()
                   .ConfigureHttpClient(ApplyUserAgent)
                   .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                   .AddStandardResilienceHandler();

    /// <summary>
    /// A typed client for the transfers themselves, deliberately without the
    /// standard resilience handler: its total-request timeout would abort a long
    /// download partway through, and a retry would restart a multi-megabyte body
    /// from zero.
    /// </summary>
    private static void AddTransferClient<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TImplementation>(IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
        => services.AddHttpClient<TInterface, TImplementation>()
                   .ConfigureHttpClient(ApplyTransferSettings)
                   .ConfigurePrimaryHttpMessageHandler(CreateHandler);

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
