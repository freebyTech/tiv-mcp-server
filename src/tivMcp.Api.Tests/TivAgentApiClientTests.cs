using System.Net;
using ModelContextProtocol;
using tivMcp.Api.Authorization;
using tivMcp.Api.Options;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The client for the agent API: it must call the configured agent API as the signed-in user, send
/// the body as given, hand the answer back untouched, and turn every failure into a clear tool error.
/// </summary>
public class TivAgentApiClientTests
{
  private const string BaseUrl = "https://agent.example.test/agent";

  private sealed class FixedTokenSource(string? token) : IUserTokenSource
  {
    public string GetApiAccessToken() => token ?? throw new UserTokenUnavailableException("no caller");
  }

  private static TivAgentApiClient Client(FakeTivApi agentApi, string? token = "the-users-token") =>
    new(
      new FakeHttpClientFactory(agentApi),
      Microsoft.Extensions.Options.Options.Create(new McpAppOptions { TivAgentApiBaseUrl = BaseUrl }),
      new FixedTokenSource(token),
      new FixedCallReference(),
      new RecordingLogger<TivAgentApiClient>()
    );

  [Fact]
  public async Task Post_calls_the_path_under_the_configured_base_url()
  {
    var agentApi = FakeTivApi.Returning("{}");

    await Client(agentApi).PostJsonAsync("budget-agent/summarize-budget", "{}");

    Assert.Equal(HttpMethod.Post, agentApi.SingleRequest.Method);
    Assert.Equal("https://agent.example.test/agent/budget-agent/summarize-budget", agentApi.SingleRequest.RequestUri!.AbsoluteUri);
  }

  [Fact]
  public async Task Post_presents_the_signed_in_users_token()
  {
    var agentApi = FakeTivApi.Returning("{}");

    await Client(agentApi, "abc.def.ghi").PostJsonAsync("budget-agent/summarize-budget", "{}");

    var authorization = agentApi.SingleRequest.Headers.Authorization;
    Assert.NotNull(authorization);
    Assert.Equal("Bearer", authorization.Scheme);
    Assert.Equal("abc.def.ghi", authorization.Parameter);
  }

  [Fact]
  public async Task Post_sends_the_body_as_given_and_as_json()
  {
    const string body = """{"budget":{"description":"default","note":"café & <b>"}}""";
    string? sent = null;
    string? contentType = null;
    var agentApi = new FakeTivApi(request =>
    {
      sent = request.Content!.ReadAsStringAsync().Result;
      contentType = request.Content.Headers.ContentType?.MediaType;
      return FakeTivApi.Json("{}");
    });

    await Client(agentApi).PostJsonAsync("budget-agent/summarize-budget", body);

    Assert.Equal(body, sent);
    Assert.Equal("application/json", contentType);
  }

  [Fact]
  public async Task Post_returns_the_answer_untouched()
  {
    const string answer = """{"summary":["Budget: default 2026-11"]}""";

    var result = await Client(FakeTivApi.Returning(answer)).PostJsonAsync("budget-agent/summarize-budget", "{}");

    Assert.Equal(answer, result);
  }

  [Fact]
  public async Task Without_a_signed_in_user_nothing_is_sent()
  {
    var agentApi = FakeTivApi.Returning("{}");

    var error = await Assert.ThrowsAnyAsync<McpException>(() => Client(agentApi, token: null).PostJsonAsync("budget-agent/summarize-budget", "{}"));

    Assert.Contains("did not accept this sign-in", error.Message);
    Assert.Empty(agentApi.Requests);
  }

  [Fact]
  public async Task A_rejected_token_tells_the_user_to_reconnect()
  {
    var agentApi = FakeTivApi.Returning("""{"detail":"Invalid audience"}""", HttpStatusCode.Unauthorized);

    var error = await Assert.ThrowsAnyAsync<McpException>(() => Client(agentApi).PostJsonAsync("budget-agent/summarize-budget", "{}"));

    Assert.Contains("Disconnect and reconnect", error.Message);
  }

  [Fact]
  public async Task A_refusal_carries_the_agent_apis_own_reason()
  {
    var agentApi = FakeTivApi.Returning("""{"detail":"Unknown tool 'drop_database'."}""", HttpStatusCode.BadRequest);

    var error = await Assert.ThrowsAnyAsync<McpException>(() => Client(agentApi).PostJsonAsync("budget-agent/apply-tool", "{}"));

    Assert.Contains("Unknown tool 'drop_database'.", error.Message);
  }

  [Fact]
  public async Task A_server_fault_does_not_leak_its_detail()
  {
    var agentApi = FakeTivApi.Returning("""{"detail":"Budget summarization failed: KeyError 'budgetCategories'"}""", HttpStatusCode.InternalServerError);

    var error = await Assert.ThrowsAnyAsync<McpException>(() => Client(agentApi).PostJsonAsync("budget-agent/summarize-budget", "{}"));

    Assert.DoesNotContain("KeyError", error.Message);
    Assert.Contains("status 500", error.Message);
  }
}
