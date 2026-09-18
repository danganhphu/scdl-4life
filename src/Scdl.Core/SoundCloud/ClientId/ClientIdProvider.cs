using System.Text.RegularExpressions;
using Scdl.Core.Results;
using static Scdl.Core.SoundCloud.ClientId.ClientIdProviderLoggers;

namespace Scdl.Core.SoundCloud.ClientId;

/// <summary>
/// Scrapes the <c>client_id</c> out of the SoundCloud web player's script
/// bundles and caches it on disk. Scraping is the only option: api-v2 requires
/// the id and SoundCloud does not issue one for this use.
/// </summary>
internal sealed partial class ClientIdProvider(HttpClient http,
                                               IOptions<SoundCloudOptions> options,
                                               TimeProvider clock,
                                               ILogger<ClientIdProvider> logger) : IClientIdProvider, IDisposable
{
    private static readonly Uri HomePageUri = new("https://soundcloud.com/");

    private readonly SoundCloudOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cached;

    // The bundle list is walked newest first; the id lives in one of the late ones.
    [GeneratedRegex("""src="(?<url>https://a-v2\.sndcdn\.com/assets/[^"]+\.js)""", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagPattern { get; }

    // The quotes around the id are written as \x22 so the raw string neither
    // begins nor ends with a quote, which no delimiter length handles cleanly.
    [GeneratedRegex("""client_id\s*[:=]\s*\x22(?<id>[a-zA-Z0-9]{32})\x22""")]
    private static partial Regex ClientIdPattern { get; }

    private static string CachePath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "scdl",
            "client-id.json");

    public async ValueTask<string> GetAsync(CancellationToken cancellationToken)
    {
        if (_options.ClientId is { Length: > 0 } configured)
        {
            return configured;
        }

        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-check: another caller may have populated it while we queued.
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = ReadCache() ?? await ScrapeAndCacheAsync(cancellationToken).ConfigureAwait(false);

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
        => _gate.Dispose();

    private async Task<string> ScrapeAndCacheAsync(CancellationToken cancellationToken)
    {
        var scraped = await ScrapeAsync(cancellationToken).ConfigureAwait(false);
        WriteCache(scraped);

        return scraped;
    }

    private async Task<string> ScrapeAsync(CancellationToken cancellationToken)
    {
        var home = await http.GetStringAsync(HomePageUri, cancellationToken).ConfigureAwait(false);

        var bundles = ScriptTagPattern
                      .Matches(home)
                      .Select(match => match.Groups["url"].Value)
                      .Distinct(StringComparer.Ordinal)
                      .Reverse()
                      .ToArray();

        LogBundlesFound(logger, bundles.Length);

        foreach (var bundle in bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string script;

            try
            {
                script = await http.GetStringAsync(bundle, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                LogBundleUnreachable(logger, bundle, e.Message);

                continue;
            }

            if (ClientIdPattern.Match(script) is { Success: true } match)
            {
                LogClientIdScraped(logger, bundle);

                return match.Groups["id"].Value;
            }
        }

        throw new ScdlException(
            "Could not find a client_id in the SoundCloud web player bundles. The page layout likely changed.",
            ScdlErrorCode.ClientIdUnavailable);
    }

    private string? ReadCache()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            var cached = JsonSerializer.Deserialize(
                File.ReadAllText(CachePath),
                SoundCloudJsonContext.Default.CachedClientId);

            if (cached is null || clock.GetUtcNow() - cached.RetrievedAt > _options.ClientIdCacheLifetime)
            {
                return null;
            }

            LogCacheHit(logger);

            return cached.ClientId;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged cache is not worth failing over; re-scrape instead.
            LogCacheUnreadable(logger, e.Message);

            return null;
        }
    }

    private void WriteCache(string clientId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

            var payload = new CachedClientId { ClientId = clientId, RetrievedAt = clock.GetUtcNow() };

            File.WriteAllText(
                CachePath,
                JsonSerializer.Serialize(payload, SoundCloudJsonContext.Default.CachedClientId));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Caching is an optimisation, never a requirement.
            LogCacheUnwritable(logger, e.Message);
        }
    }
}
