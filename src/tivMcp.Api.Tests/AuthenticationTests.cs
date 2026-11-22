using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The server is an OAuth protected resource: every MCP request needs a token issued by the
/// configured authority FOR THIS SERVER, and an assistant with no token must be told where to get
/// one. These run the real pipeline end to end on an in-memory server.
/// </summary>
public class AuthenticationTests
{
  private const string InitializeRequest = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}""";

  private static HttpRequestMessage McpInitialize(string? token = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(InitializeRequest, Encoding.UTF8, "application/json") };
    request.Headers.Accept.ParseAdd("application/json");
    request.Headers.Accept.ParseAdd("text/event-stream");
    if (token != null)
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    return request;
  }

  [Fact]
  public async Task Health_probe_needs_no_token()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Mcp_request_without_a_token_is_challenged_with_the_metadata_location()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.SendAsync(McpInitialize());

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var challenge = response.Headers.WwwAuthenticate.ToString();
    Assert.StartsWith("Bearer", challenge);
    Assert.Contains("resource_metadata=", challenge);
    Assert.Contains("/.well-known/oauth-protected-resource", challenge);
  }

  [Fact]
  public async Task Challenge_behind_a_tls_terminating_proxy_points_at_https()
  {
    // In the cluster the ingress ends TLS and calls the server over plain http, saying so in
    // X-Forwarded-Proto. The metadata location handed to the assistant must be the https one.
    await using var host = await McpTestHost.StartAsync();
    var request = McpInitialize();
    request.Headers.Add("X-Forwarded-Proto", "https");

    var response = await host.Client.SendAsync(request);

    var metadataUrl = Regex.Match(response.Headers.WwwAuthenticate.ToString(), "resource_metadata=\"([^\"]+)\"").Groups[1].Value;
    Assert.StartsWith("https://", metadataUrl);
  }

  [Fact]
  public async Task Forwarded_host_is_not_trusted()
  {
    // Only the scheme is taken from forwarded headers; a caller cannot make the server point
    // assistants at another host.
    await using var host = await McpTestHost.StartAsync();
    var request = McpInitialize();
    request.Headers.Add("X-Forwarded-Host", "evil.example.test");

    var response = await host.Client.SendAsync(request);

    Assert.DoesNotContain("evil.example.test", response.Headers.WwwAuthenticate.ToString());
  }

  [Fact]
  public async Task Protected_resource_metadata_names_this_server_the_authority_and_the_scopes()
  {
    await using var host = await McpTestHost.StartAsync();

    // Follow the pointer exactly as an assistant would: from the 401 challenge to the document.
    var challenged = await host.Client.SendAsync(McpInitialize());
    var metadataUrl = Regex.Match(challenged.Headers.WwwAuthenticate.ToString(), "resource_metadata=\"([^\"]+)\"").Groups[1].Value;
    Assert.NotEqual(string.Empty, metadataUrl);

    var response = await host.Client.GetAsync(new Uri(metadataUrl).PathAndQuery);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = document.RootElement;
    Assert.Equal(McpTestHost.Audience, root.GetProperty("resource").GetString());
    Assert.Equal([McpTestHost.Authority], root.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()));
    Assert.Equal(["data:read", "data:write"], root.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
  }

  [Fact]
  public async Task Token_issued_for_this_server_is_accepted()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.SendAsync(McpInitialize(McpTestHost.CreateToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("\"serverInfo\"", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Token_issued_for_another_audience_is_rejected()
  {
    // The core audience-binding rule: a perfectly valid token from the same authority, but issued
    // for a different API (for example the trackItsValue API itself), must not open this server.
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.SendAsync(McpInitialize(McpTestHost.CreateToken(audience: "some-other-api")));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Token_from_another_issuer_is_rejected()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.SendAsync(McpInitialize(McpTestHost.CreateToken(issuer: "https://someone-else.example.test/")));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Expired_token_is_rejected()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await host.Client.SendAsync(McpInitialize(McpTestHost.CreateToken(expires: DateTime.UtcNow.AddMinutes(-30))));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Token_signed_with_an_unknown_key_is_rejected()
  {
    await using var host = await McpTestHost.StartAsync();
    var forgedKey = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" };

    var response = await host.Client.SendAsync(McpInitialize(McpTestHost.CreateToken(signingKey: forgedKey)));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
