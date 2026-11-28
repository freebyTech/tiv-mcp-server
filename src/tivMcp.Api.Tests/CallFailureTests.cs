using System.Net;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using tivMcp.Api.Authorization;
using tivMcp.Api.Options;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// A failed call must be traceable: the user is told which service failed and given a reference,
/// every request carries that reference, and this server's log holds the detail under it. The
/// detail itself stays out of what the user sees.
/// </summary>
public class CallFailureTests
{
  private sealed class FixedTokenSource : IUserTokenSource
  {
    public string GetApiAccessToken() => "the-users-token";
  }

  private readonly RecordingLogger<TivApiClient> _apiLog = new();
  private readonly RecordingLogger<TivAgentApiClient> _agentLog = new();

  private TivApiClient Api(FakeTivApi api) =>
    new(
      new FakeHttpClientFactory(api),
      Microsoft.Extensions.Options.Options.Create(new McpAppOptions { TivApiBaseUrl = "https://api.example.test/tiv" }),
      new FixedTokenSource(),
      new FixedCallReference(),
      _apiLog
    );

  private TivAgentApiClient AgentApi(FakeTivApi api) =>
    new(
      new FakeHttpClientFactory(api),
      Microsoft.Extensions.Options.Options.Create(new McpAppOptions { TivAgentApiBaseUrl = "https://agent.example.test/agent" }),
      new FixedTokenSource(),
      new FixedCallReference(),
      _agentLog
    );

  [Fact]
  public void A_reference_is_eight_hex_characters_and_differs_between_tool_calls()
  {
    var first = new CallReference();
    var second = new CallReference();

    Assert.Matches("^[0-9a-f]{8}$", first.Value);
    Assert.Equal(first.Value, first.Value);
    Assert.NotEqual(first.Value, second.Value);
  }

  [Fact]
  public async Task Every_request_to_the_api_carries_the_tool_calls_reference()
  {
    var api = FakeTivApi.Returning("{}");

    await Api(api).GetJsonAsync("user/status");
    await Api(api).PutJsonAsync("budgeting/budgets", "{}");
    await Api(api).PostJsonAsync("budgeting/budgets/copy", "{}");

    Assert.All(api.Requests, request => Assert.Equal("ab12cd34", Assert.Single(request.Headers.GetValues("X-Request-Id"))));
  }

  [Fact]
  public async Task Every_request_to_the_agent_api_carries_the_tool_calls_reference()
  {
    var agentApi = FakeTivApi.Returning("{}");

    await AgentApi(agentApi).PostJsonAsync("budget-agent/apply-tool", "{}");

    Assert.Equal("ab12cd34", Assert.Single(agentApi.SingleRequest.Headers.GetValues("X-Request-Id")));
  }

  [Fact]
  public async Task A_server_fault_names_the_service_and_gives_the_reference()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":500,"Message":"Internal Server Error"}""", HttpStatusCode.InternalServerError);

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).PutJsonAsync("budgeting/budgets", "{}"));

    Assert.Equal("trackItsValue ran into a problem of its own (status 500). Try again later. Reference ab12cd34 (budget API).", error.Message);
    Assert.Equal(500, error.StatusCode);
    Assert.Equal("budget API", error.Service);
    Assert.Equal("ab12cd34", error.Reference);
    Assert.True(error.OutcomeUnknown);
  }

  [Fact]
  public async Task A_server_fault_is_logged_with_its_reference_call_status_and_body()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":500,"Message":"Internal Server Error"}""", HttpStatusCode.InternalServerError);

    await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).PutJsonAsync("budgeting/budgets", "{}"));

    var entry = Assert.Single(_apiLog.Entries);
    Assert.Equal(LogLevel.Warning, entry.Level);
    Assert.StartsWith("Call ab12cd34 to the budget API failed: PUT budgeting/budgets answered 500 after ", entry.Message);
    Assert.EndsWith("""{"StatusCode":500,"Message":"Internal Server Error"}""", entry.Message);
  }

  [Fact]
  public async Task The_agent_apis_detail_goes_to_the_log_and_not_to_the_user()
  {
    var agentApi = FakeTivApi.Returning("""{"detail":"Tool failed: KeyError 'budgetCategories'"}""", HttpStatusCode.InternalServerError);

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => AgentApi(agentApi).PostJsonAsync("budget-agent/apply-tool", "{}"));

    Assert.DoesNotContain("KeyError", error.Message);
    Assert.EndsWith("Reference ab12cd34 (agent API).", error.Message);
    var entry = Assert.Single(_agentLog.Entries);
    Assert.Contains("POST budget-agent/apply-tool answered 500", entry.Message);
    Assert.Contains("KeyError 'budgetCategories'", entry.Message);
  }

  [Fact]
  public async Task A_long_body_is_cut_short_in_the_log()
  {
    var api = FakeTivApi.Returning(new string('x', 5000), HttpStatusCode.InternalServerError);

    await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).GetJsonAsync("user/status"));

    Assert.InRange(Assert.Single(_apiLog.Entries).Message.Length, 1000, 1200);
  }

  [Fact]
  public async Task The_users_token_is_never_logged()
  {
    var api = FakeTivApi.Returning("nope", HttpStatusCode.InternalServerError);

    await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).GetJsonAsync("user/status"));

    Assert.DoesNotContain("the-users-token", Assert.Single(_apiLog.Entries).Message);
  }

  [Fact]
  public async Task No_answer_at_all_is_logged_with_the_cause_and_shown_with_the_reference()
  {
    var api = new FakeTivApi(_ => throw new HttpRequestException("No such host is known. (internal-host.cluster.local:443)"));

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).GetJsonAsync("user/status"));

    Assert.Equal("trackItsValue could not be reached. Try again in a moment. Reference ab12cd34 (budget API).", error.Message);
    Assert.Equal(0, error.StatusCode);
    Assert.True(error.OutcomeUnknown);
    var entry = Assert.Single(_apiLog.Entries);
    Assert.Contains("GET user/status answered 0", entry.Message);
    Assert.Contains("internal-host.cluster.local", entry.Message);
  }

  [Fact]
  public async Task A_refusal_that_explains_itself_needs_no_reference_but_is_still_logged()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":403,"Message":"This feature is unlocked at level 4 (price trending)."}""", HttpStatusCode.Forbidden);

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).GetJsonAsync("budgeting/lineage/groups"));

    Assert.Equal("trackItsValue refused this request: This feature is unlocked at level 4 (price trending).", error.Message);
    Assert.False(error.OutcomeUnknown);
    Assert.Contains("answered 403", Assert.Single(_apiLog.Entries).Message);
  }

  [Fact]
  public async Task A_refusal_with_no_reason_gets_the_reference()
  {
    var api = FakeTivApi.Returning("", HttpStatusCode.BadRequest);

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => Api(api).PutJsonAsync("budgeting/budgets", "{}"));

    Assert.Equal("trackItsValue could not process this request (status 400). Reference ab12cd34 (budget API).", error.Message);
  }

  [Fact]
  public async Task A_failure_is_still_an_ordinary_tool_error()
  {
    var api = FakeTivApi.Returning("", HttpStatusCode.InternalServerError);

    await Assert.ThrowsAnyAsync<McpException>(() => Api(api).GetJsonAsync("user/status"));
  }

  [Fact]
  public async Task Nothing_is_logged_when_a_call_succeeds()
  {
    await Api(FakeTivApi.Returning("{}")).GetJsonAsync("user/status");

    Assert.Empty(_apiLog.Entries);
  }

  [Theory]
  [InlineData(0, false, "trackItsValue could not be reached. Try again in a moment.")]
  [InlineData(0, true, "trackItsValue did not answer in time. Try again in a moment.")]
  [InlineData(502, false, "trackItsValue could not be reached through its gateway (status 502); it may be restarting. Try again in a moment.")]
  [InlineData(504, false, "trackItsValue could not be reached through its gateway (status 504); it may be restarting. Try again in a moment.")]
  [InlineData(503, false, "trackItsValue is busy or restarting (status 503). Try again in a moment.")]
  [InlineData(500, false, "trackItsValue ran into a problem of its own (status 500). Try again later.")]
  public void Each_kind_of_server_trouble_has_its_own_sentence(int statusCode, bool timedOut, string expected)
  {
    Assert.Equal(expected, TivApiErrorMapper.Describe(statusCode, "<html>gateway detail</html>", timedOut));
  }

  [Theory]
  [InlineData(0, null, true)]
  [InlineData(500, """{"Message":"Internal Server Error"}""", true)]
  [InlineData(503, "", true)]
  [InlineData(400, "", true)]
  [InlineData(404, "", true)]
  [InlineData(400, """{"errors":{"date":["The date field is required."]}}""", false)]
  [InlineData(403, """{"Message":"This feature needs a paid plan."}""", false)]
  [InlineData(409, """{"Message":"A budget named 'default' already exists for 2026-12."}""", false)]
  [InlineData(401, "", false)]
  [InlineData(429, "", false)]
  public void Only_failures_that_do_not_explain_themselves_are_shown_with_a_reference(int statusCode, string? body, bool unexplained)
  {
    Assert.Equal(unexplained, TivApiErrorMapper.IsUnexplained(statusCode, body));
  }
}
