using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// Tool calls through the whole server, as an assistant makes them: a JSON-RPC request with the
/// user's token comes in on /mcp, and a request to the trackItsValue API goes out. Only the API
/// itself is a stand-in.
/// </summary>
public class ToolsEndToEndTests
{
  private static readonly string[] ExpectedTools =
  [
    "export_data",
    "get_accounts_overview",
    "get_budget",
    "get_server_info",
    "get_total_wealth_outlook",
    "get_user_status",
    "list_accounts",
    "list_budgets",
  ];

  private static async Task<JsonElement> Rpc(McpTestHost host, string token, string method, object? parameters = null)
  {
    var body = JsonSerializer.Serialize(
      new
      {
        jsonrpc = "2.0",
        id = 1,
        method,
        @params = parameters ?? new { },
      }
    );
    var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    request.Headers.Accept.ParseAdd("application/json");
    request.Headers.Accept.ParseAdd("text/event-stream");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await host.Client.SendAsync(request);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // The server may answer as plain JSON or as a one-message event stream.
    var text = await response.Content.ReadAsStringAsync();
    if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
    {
      text = text.Split('\n').Last(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..];
    }
    return JsonDocument.Parse(text).RootElement.Clone();
  }

  private static Task<JsonElement> CallTool(McpTestHost host, string token, string name, object arguments) => Rpc(host, token, "tools/call", new { name, arguments });

  private static string ResultText(JsonElement response) => response.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

  private static bool IsError(JsonElement response) => response.GetProperty("result").TryGetProperty("isError", out var isError) && isError.GetBoolean();

  [Fact]
  public async Task The_server_offers_exactly_the_documented_tools()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(host, McpTestHost.CreateToken(), "tools/list");

    var names = response.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).Order();
    Assert.Equal(ExpectedTools, names);
  }

  [Fact]
  public async Task Every_tool_is_announced_to_assistants_as_read_only()
  {
    // Phase 1 has no write tools. Assistants use these hints to decide whether to ask the user
    // first, and a read-only connection must be able to use every tool offered.
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(host, McpTestHost.CreateToken(), "tools/list");

    foreach (var tool in response.GetProperty("result").GetProperty("tools").EnumerateArray())
    {
      var annotations = tool.GetProperty("annotations");
      Assert.True(annotations.GetProperty("readOnlyHint").GetBoolean(), tool.GetProperty("name").GetString());
      Assert.False(annotations.GetProperty("destructiveHint").GetBoolean(), tool.GetProperty("name").GetString());
      Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString()));
    }
  }

  [Fact]
  public void No_tool_method_is_missing_its_read_only_marking()
  {
    var toolMethods = typeof(Startup)
      .Assembly.GetTypes()
      .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
      .SelectMany(type => type.GetMethods())
      .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
      .Where(attribute => attribute != null)
      .ToList();

    Assert.Equal(ExpectedTools.Length, toolMethods.Count);
    Assert.All(
      toolMethods,
      attribute =>
      {
        Assert.True(attribute!.ReadOnly, attribute.Name);
        Assert.False(attribute.Destructive, attribute.Name);
      }
    );
  }

  [Fact]
  public async Task A_tool_call_reaches_the_api_as_the_signed_in_user_and_returns_its_answer()
  {
    const string budgets = """[{"description":"default","year":2026,"month":11}]""";
    var api = FakeTivApi.Returning(budgets);
    await using var host = await McpTestHost.StartAsync(api);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(host, token, "list_budgets", new { count = 3 });

    Assert.False(IsError(response));
    Assert.Equal(budgets, ResultText(response));
    Assert.Equal(HttpMethod.Get, api.SingleRequest.Method);
    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/budgeting/budgets/list/3", api.SingleRequest.RequestUri!.AbsoluteUri);
    // The API must see the very token the assistant presented, so it resolves the same user.
    Assert.Equal(token, api.SingleRequest.Headers.Authorization!.Parameter);
  }

  [Fact]
  public async Task Arguments_arrive_by_name_and_optional_ones_may_be_left_out()
  {
    var api = FakeTivApi.Returning("{}");
    await using var host = await McpTestHost.StartAsync(api);

    await CallTool(host, McpTestHost.CreateToken(), "get_budget", new { year = 2026, month = 7 });
    await CallTool(host, McpTestHost.CreateToken(), "get_total_wealth_outlook", new { range = "1yr" });

    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/budgeting/budgets/default/2026/7", api.Requests[0].RequestUri!.AbsoluteUri);
    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/dashboards/total-wealth?range=1yr", api.Requests[1].RequestUri!.AbsoluteUri);
  }

  [Fact]
  public async Task A_refusal_from_the_api_comes_back_as_a_tool_error_the_user_can_read()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":403,"Message":"This action requires user level 3 (you're at 1)."}""", HttpStatusCode.Forbidden);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_total_wealth_outlook", new { });

    Assert.True(IsError(response));
    Assert.Contains("trackItsValue refused this request: This action requires user level 3 (you're at 1).", ResultText(response));
  }

  [Fact]
  public async Task A_bad_argument_comes_back_as_a_tool_error_and_nothing_is_sent_to_the_api()
  {
    var api = FakeTivApi.Returning("{}");
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_budget", new { year = 2026, month = 13 });

    Assert.True(IsError(response));
    Assert.Contains("month must be between 1 (January) and 12 (December)", ResultText(response));
    Assert.Empty(api.Requests);
  }

  [Fact]
  public async Task A_server_fault_in_the_api_does_not_leak_its_detail_to_the_assistant()
  {
    var api = FakeTivApi.Returning("""{"StatusCode":500,"Message":"SqlException: Login failed for user 'sa'."}""", HttpStatusCode.InternalServerError);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "list_accounts", new { });

    Assert.True(IsError(response));
    Assert.DoesNotContain("SqlException", response.GetRawText());
  }
}
