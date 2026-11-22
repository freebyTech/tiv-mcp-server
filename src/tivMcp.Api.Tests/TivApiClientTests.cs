using System.Net;
using ModelContextProtocol;
using tivMcp.Api.Authorization;
using tivMcp.Api.Options;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The client every tool goes through: it must call the configured API as the signed-in user, hand
/// the body back untouched, and turn every failure into a clear tool error.
/// </summary>
public class TivApiClientTests
{
  private const string BaseUrl = "https://api.example.test/tiv";

  private sealed class FixedTokenSource(string? token) : IUserTokenSource
  {
    public string GetApiAccessToken() => token ?? throw new UserTokenUnavailableException("no caller");
  }

  private static TivApiClient Client(FakeTivApi api, string? token = "the-users-token") =>
    new(new FakeHttpClientFactory(api), Microsoft.Extensions.Options.Options.Create(new McpAppOptions { TivApiBaseUrl = BaseUrl }), new FixedTokenSource(token));

  [Fact]
  public async Task Get_calls_the_path_under_the_configured_base_url()
  {
    var api = FakeTivApi.Returning("[]");

    await Client(api).GetJsonAsync("budgeting/budgets/list/12");

    Assert.Equal(HttpMethod.Get, api.SingleRequest.Method);
    Assert.Equal("https://api.example.test/tiv/budgeting/budgets/list/12", api.SingleRequest.RequestUri!.AbsoluteUri);
  }

  [Fact]
  public async Task Get_presents_the_signed_in_users_token()
  {
    var api = FakeTivApi.Returning("[]");

    await Client(api, "abc.def.ghi").GetJsonAsync("user/status");

    var authorization = api.SingleRequest.Headers.Authorization;
    Assert.NotNull(authorization);
    Assert.Equal("Bearer", authorization.Scheme);
    Assert.Equal("abc.def.ghi", authorization.Parameter);
  }

  [Fact]
  public async Task Get_sends_query_parameters()
  {
    var api = FakeTivApi.Returning("{}");

    await Client(api).GetJsonAsync("dashboards/total-wealth", new Dictionary<string, string> { ["range"] = "1yr" });

    Assert.Equal("https://api.example.test/tiv/dashboards/total-wealth?range=1yr", api.SingleRequest.RequestUri!.AbsoluteUri);
  }

  [Fact]
  public async Task Get_returns_the_body_exactly_as_the_api_wrote_it()
  {
    const string body = """{"description":"default","year":2026,"amount":12.50,"unknownFutureField":null}""";

    var result = await Client(FakeTivApi.Returning(body)).GetJsonAsync("anything");

    Assert.Equal(body, result);
  }

  [Fact]
  public async Task GetBytes_returns_the_raw_bytes()
  {
    byte[] bytes = [0x50, 0x4B, 0x03, 0x04, 0x00, 0xFF];
    var api = new FakeTivApi(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });

    var result = await Client(api).GetBytesAsync("export");

    Assert.Equal(bytes, result);
    Assert.Equal("Bearer", api.SingleRequest.Headers.Authorization!.Scheme);
  }

  [Fact]
  public async Task A_refusal_from_the_api_becomes_a_tool_error_with_the_apis_reason()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":403,"Message":"This action requires user level 3 (you're at 1)."}""", HttpStatusCode.Forbidden);

    var error = await Assert.ThrowsAsync<McpException>(() => Client(api).GetJsonAsync("dashboards/total-wealth"));

    Assert.Equal("trackItsValue refused this request: This action requires user level 3 (you're at 1).", error.Message);
  }

  [Fact]
  public async Task A_401_from_the_api_becomes_a_reconnect_message()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":401,"Message":"Missing required authorization information."}""", HttpStatusCode.Unauthorized);

    var error = await Assert.ThrowsAsync<McpException>(() => Client(api).GetJsonAsync("user/status"));

    Assert.Contains("did not accept this sign-in", error.Message);
  }

  [Fact]
  public async Task A_network_failure_becomes_an_unreachable_message_without_internal_detail()
  {
    var api = new FakeTivApi(_ => throw new HttpRequestException("No such host is known. (internal-host.cluster.local:443)"));

    var error = await Assert.ThrowsAsync<McpException>(() => Client(api).GetJsonAsync("user/status"));

    Assert.Contains("could not be reached", error.Message);
    Assert.DoesNotContain("internal-host", error.Message);
  }

  [Fact]
  public async Task With_no_signed_in_caller_nothing_is_sent()
  {
    var api = FakeTivApi.Returning("{}");

    var error = await Assert.ThrowsAsync<McpException>(() => Client(api, token: null).GetJsonAsync("user/status"));

    Assert.Empty(api.Requests);
    Assert.Contains("did not accept this sign-in", error.Message);
  }
}
