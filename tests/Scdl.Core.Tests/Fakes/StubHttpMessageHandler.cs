using System.Net;
using System.Text;

namespace Scdl.Core.Tests.Fakes;

/// <summary>
/// A hand written handler rather than a Moq one. <c>HttpMessageHandler.SendAsync</c>
/// is protected, so mocking it means <c>Protected().Setup&lt;...&gt;("SendAsync", ...)</c>,
/// which matches by string name and breaks silently if the signature moves.
/// Moq is still used in this suite wherever the seam is a real interface.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static StubHttpMessageHandler ReturningJson(string json)
        => new(_ => JsonResponse(json));

    /// <summary>Responds per absolute URI, matched by <c>Contains</c> in registration order.</summary>
    public static StubHttpMessageHandler Routing(params (string UriFragment, string Json)[] routes)
        => new(request =>
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

            foreach (var (fragment, json) in routes)
            {
                if (uri.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonResponse(json);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    public HttpClient CreateClient()
        => new(this, disposeHandler: false) { BaseAddress = new Uri("https://api-v2.soundcloud.com") };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri)
        {
            Requests.Add(uri);
        }

        return Task.FromResult(responder(request));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
