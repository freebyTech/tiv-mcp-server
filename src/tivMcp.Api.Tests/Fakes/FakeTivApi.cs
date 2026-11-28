using System.Net;
using System.Text;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the trackItsValue API at the HTTP level. It records every request the server
/// sends and answers with whatever the test supplies, so tests can assert on the exact URL and
/// headers that would have gone over the network.
/// </summary>
internal sealed class FakeTivApi : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

  public List<HttpRequestMessage> Requests { get; } = [];

  /// <summary>
  /// The usage checks and reports the server made around tool calls. They are kept apart from
  /// <see cref="Requests"/> so a test about a tool sees only that tool's own calls.
  /// </summary>
  public List<HttpRequestMessage> UsageRequests { get; } = [];

  /// <summary>Answers the usage checks and reports; by default every call is allowed.</summary>
  public Func<HttpRequestMessage, HttpResponseMessage> RespondToUsage { get; set; } = _ => Json("""{"allowed":true,"tier":"free"}""");

  public FakeTivApi(Func<HttpRequestMessage, HttpResponseMessage> respond)
  {
    _respond = respond;
  }

  /// <summary>Answers every request with the same status and JSON body.</summary>
  public static FakeTivApi Returning(string json, HttpStatusCode statusCode = HttpStatusCode.OK) => new(_ => Json(json, statusCode));

  public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

  public HttpRequestMessage SingleRequest => Assert.Single(Requests);

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    if (request.RequestUri!.AbsolutePath.Contains("/usage/", StringComparison.Ordinal))
    {
      lock (UsageRequests)
      {
        UsageRequests.Add(request);
      }
      return Task.FromResult(RespondToUsage(request));
    }

    Requests.Add(request);
    return Task.FromResult(_respond(request));
  }
}

/// <summary>
/// Hands out HttpClients backed by a <see cref="FakeTivApi"/>, for constructing the real
/// TivApiClient without a host.
/// </summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
  private readonly HttpMessageHandler _handler;

  public FakeHttpClientFactory(HttpMessageHandler handler)
  {
    _handler = handler;
  }

  public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
