using System.Text.RegularExpressions;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// Scrapes the <c>client_id</c> out of the SoundCloud web player's script
/// bundles and caches it on disk. Scraping is the only option: api-v2 requires
/// the id and SoundCloud does not issue one for this use.
/// </summary>
public sealed partial class ClientIdProvider : IClientIdProvider, IDisposable
{
    private static readonly Uri HomePageUri = new("https://soundcloud.com/");

    private readonly HttpClient _http;
    private readonly SoundCloudOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ClientIdProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cached;

    public ClientIdProvider(HttpClient http,
                            IOptions<SoundCloudOptions> options,
                            TimeProvider clock,
                            ILogger<ClientIdProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    // The bundle list is walked newest first; the id lives in one of the late ones.
    [GeneratedRegex("""src="(?<url>https://a-v2\.sndcdn\.com/assets/[^"]+\.js)""", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagPattern { get; }

    // The quotes around the id are written as \x22 so the raw string does not
    // begin or end with a quote character, which no delimiter length handles
    // cleanly.
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

    private async Task<string> ScrapeAndCacheAsync(CancellationToken cancellationToken)
    {
        var scraped = await ScrapeAsync(cancellationToken).ConfigureAwait(false);
        WriteCache(scraped);

        return scraped;
    }

    private async Task<string> ScrapeAsync(CancellationToken cancellationToken)
    {
        var home = await _http.GetStringAsync(HomePageUri, cancellationToken).ConfigureAwait(false);

        var bundles = ScriptTagPattern
                      .Matches(home)
                      .Select(match => match.Groups["url"].Value)
                      .Distinct(StringComparer.Ordinal)
                      .Reverse()
                      .ToArray();

        LogBundlesFound(bundles.Length);

        foreach (var bundle in bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string script;

            try
            {
                script = await _http.GetStringAsync(bundle, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                LogBundleUnreachable(bundle, e.Message);

                continue;
            }

            if (ClientIdPattern.Match(script) is { Success: true } match)
            {
                LogClientIdScraped(bundle);

                return match.Groups["id"].Value;
            }
        }

        throw new ScdlException(
            "Could not find a client_id in the SoundCloud web player bundles. The page layout likely changed.");
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

            if (cached is null || _clock.GetUtcNow() - cached.RetrievedAt > _options.ClientIdCacheLifetime)
            {
                return null;
            }

            LogCacheHit();

            return cached.ClientId;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged cache is not worth failing over; re-scrape instead.
            LogCacheUnreadable(e.Message);

            return null;
        }
    }

    private void WriteCache(string clientId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

            var payload = new CachedClientId { ClientId = clientId, RetrievedAt = _clock.GetUtcNow() };

            File.WriteAllText(
                CachePath,
                JsonSerializer.Serialize(payload, SoundCloudJsonContext.Default.CachedClientId));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Caching is an optimisation, never a requirement.
            LogCacheUnwritable(e.Message);
        }
    }

    public void Dispose()
        => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} web player bundle(s) to search.")]
    private partial void LogBundlesFound(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scraped client_id from {Bundle}.")]
    private partial void LogClientIdScraped(string bundle);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bundle {Bundle} unreachable: {Reason}")]
    private partial void LogBundleUnreachable(string bundle, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using cached client_id.")]
    private partial void LogCacheHit();

    [LoggerMessage(Level = LogLevel.Debug, Message = "client_id cache unreadable: {Reason}")]
    private partial void LogCacheUnreadable(string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "client_id cache not written: {Reason}")]
    private partial void LogCacheUnwritable(string reason);
}
