using System.Net;

namespace Scdl.Core.Tests.Fakes;

/// <summary>
/// A hand written handler rather than a Moq one. <c>HttpMessageHandler.SendAsync</c>
/// is protected, so mocking it means <c>Protected().Setup&lt;...&gt;("SendAsync", ...)</c>,
/// which matches by string name and breaks silently if the signature moves.
/// Moq is still used in this suite where the seam is a real interface.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public List<Uri> Requests { get; } = [];

    public static StubHttpMessageHandler ReturningJson(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    /// <summary>Responds per absolute URI, matched by <c>Contains</c> in registration order.</summary>
    public static StubHttpMessageHandler Routing(params (string UriFragment, string Json)[] routes) =>
        new(request =>
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

            foreach (var (fragment, json) in routes)
            {
                if (uri.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    public HttpClient CreateClient() => new(this, disposeHandler: false)
    {
        BaseAddress = new Uri("https://api-v2.soundcloud.com"),
    };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri)
        {
            Requests.Add(uri);
        }

        return Task.FromResult(_responder(request));
    }
}

/// <summary>Deterministic clock, so cache expiry can be tested without waiting a week.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
