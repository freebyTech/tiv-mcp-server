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
  private static readonly string[] ReadOnlyTools =
  [
    "get_accounts_overview",
    "get_accounts_summary",
    "get_available_accounts",
    "get_budget",
    "get_budget_summary",
    "get_item_trends",
    "get_server_info",
    "get_total_wealth_outlook",
    "get_user_status",
    "list_accounts",
    "list_budgets",
    "list_trend_items",
  ];

  // Tools that change the user's data. Assistants are told so, and may ask the user first.
  private static readonly string[] WriteTools =
  [
    "add_actual",
    "add_actuals",
    "add_category",
    "add_item",
    "create_budget",
    "delete_actual",
    "delete_category",
    "delete_item",
    "move_budget",
    "rectify_budget",
    "rectify_category",
    "rectify_item",
    "update_actual",
    "update_category",
    "update_estimates",
    "update_item",
    "verify_account_transactions",
  ];

  // Write tools that only ever add something; every other write tool replaces or removes data.
  private static readonly string[] AdditiveTools = ["add_actual", "add_actuals", "add_category", "add_item", "create_budget"];

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
    Assert.Equal(ReadOnlyTools.Concat(WriteTools).Order(), names);
  }

  [Fact]
  public async Task Each_tool_is_announced_to_assistants_as_reading_or_changing_data()
  {
    // Assistants use these hints to decide whether to ask the user first, so a tool that changes
    // data must never be announced as read-only, and one that removes or replaces data must say so.
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(host, McpTestHost.CreateToken(), "tools/list");

    foreach (var tool in response.GetProperty("result").GetProperty("tools").EnumerateArray())
    {
      var name = tool.GetProperty("name").GetString()!;
      var annotations = tool.GetProperty("annotations");
      var isWrite = WriteTools.Contains(name);

      Assert.Equal(!isWrite, annotations.GetProperty("readOnlyHint").GetBoolean());
      Assert.Equal(isWrite && !AdditiveTools.Contains(name), annotations.GetProperty("destructiveHint").GetBoolean());
      Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString()));
    }
  }

  [Fact]
  public void Every_tool_method_is_listed_here_as_reading_or_changing_data()
  {
    var toolNames = typeof(Startup)
      .Assembly.GetTypes()
      .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
      .SelectMany(type => type.GetMethods())
      .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
      .Where(attribute => attribute != null)
      .Select(attribute => attribute!.Name)
      .Order();

    Assert.Equal(ReadOnlyTools.Concat(WriteTools).Order(), toolNames);
  }

  [Fact]
  public async Task A_write_tool_gets_the_data_has_the_agent_api_edit_it_and_saves_it_as_the_same_user()
  {
    const string budget = """{"id":"b-1","isNew":false,"budgetCategories":[]}""";
    const string edited = """{"id":"b-1","isDirty":true,"budgetCategories":[{"isNew":true}]}""";
    string? saved = null;
    var api = new FakeTivApi(request =>
    {
      if (request.Method == HttpMethod.Put)
      {
        saved = request.Content!.ReadAsStringAsync().Result;
        return FakeTivApi.Json(edited);
      }
      return FakeTivApi.Json(request.RequestUri!.AbsolutePath.EndsWith("/budgeting/templates") ? "[]" : budget);
    });
    string? sentToAgent = null;
    var agentApi = new FakeTivApi(request =>
    {
      sentToAgent = request.Content!.ReadAsStringAsync().Result;
      return FakeTivApi.Json("""{"success":true,"changes":[{"description":"Added 'Aldi' ($55.90) to 'Groceries'"}],"budget":""" + edited + "}");
    });
    await using var host = await McpTestHost.StartAsync(api, agentApi);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(
      host,
      token,
      "add_actual",
      new
      {
        year = 2026,
        month = 11,
        itemDescription = "Groceries",
        amount = 55.90,
        description = "Aldi",
        date = "2026-11-22",
      }
    );

    Assert.False(IsError(response));
    Assert.Equal("Saved. Added 'Aldi' ($55.90) to 'Groceries'", ResultText(response));

    Assert.Equal($"{McpTestHost.TivAgentApiBaseUrl}/budget-agent/apply-tool", agentApi.SingleRequest.RequestUri!.AbsoluteUri);
    var agentRequest = JsonDocument.Parse(sentToAgent!).RootElement;
    Assert.Equal("add_actual", agentRequest.GetProperty("tool").GetString());
    Assert.Equal("Groceries", agentRequest.GetProperty("arguments").GetProperty("item_description").GetString());
    Assert.Equal(55.90m, agentRequest.GetProperty("arguments").GetProperty("amount").GetDecimal());

    var put = Assert.Single(api.Requests, request => request.Method == HttpMethod.Put);
    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/budgeting/budgets", put.RequestUri!.AbsoluteUri);
    Assert.Equal(edited, saved);
    Assert.All(api.Requests.Append(agentApi.SingleRequest), request => Assert.Equal(token, request.Headers.Authorization!.Parameter));
  }

  [Fact]
  public async Task When_the_api_refuses_the_save_the_user_is_told_why()
  {
    var api = new FakeTivApi(request =>
      request.Method == HttpMethod.Put
        ? FakeTivApi.Json("\"This budget is closed and cannot be changed.\"", HttpStatusCode.Conflict)
        : FakeTivApi.Json(request.RequestUri!.AbsolutePath.EndsWith("/budgeting/templates") ? "[]" : """{"id":"b-1","isNew":false}""")
    );
    var agentApi = FakeTivApi.Returning("""{"success":true,"changes":[],"budget":{"id":"b-1"}}""");
    await using var host = await McpTestHost.StartAsync(api, agentApi);

    var response = await CallTool(host, McpTestHost.CreateToken(), "rectify_budget", new { year = 2026, month = 11 });

    Assert.True(IsError(response));
    Assert.Contains("This budget is closed and cannot be changed.", ResultText(response));
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
  public async Task A_summary_tool_passes_the_apis_json_to_the_agent_api_as_the_same_user()
  {
    const string budget = """{"description":"default","year":2026,"month":11,"budgetCategories":[]}""";
    var api = FakeTivApi.Returning(budget);
    string? sentToAgent = null;
    var agentApi = new FakeTivApi(request =>
    {
      sentToAgent = request.Content!.ReadAsStringAsync().Result;
      return FakeTivApi.Json("""{"summary":["Budget: default 2026-11","Income: Estimated=$0.00, Actual=$0.00"]}""");
    });
    await using var host = await McpTestHost.StartAsync(api, agentApi);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(host, token, "get_budget_summary", new { year = 2026, month = 11 });

    Assert.False(IsError(response));
    Assert.Equal("Budget: default 2026-11\nIncome: Estimated=$0.00, Actual=$0.00", ResultText(response));
    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/budgeting/budgets/default/2026/11", api.SingleRequest.RequestUri!.AbsoluteUri);
    Assert.Equal(HttpMethod.Post, agentApi.SingleRequest.Method);
    Assert.Equal($"{McpTestHost.TivAgentApiBaseUrl}/budget-agent/summarize-budget", agentApi.SingleRequest.RequestUri!.AbsoluteUri);
    Assert.Equal($$"""{"budget":{{budget}}}""", sentToAgent);
    Assert.Equal(token, agentApi.SingleRequest.Headers.Authorization!.Parameter);
  }

  [Fact]
  public async Task A_refusal_from_the_agent_api_comes_back_as_a_tool_error_and_does_not_hide_the_reason()
  {
    var api = FakeTivApi.Returning("{}");
    var agentApi = FakeTivApi.Returning("""{"detail":"Invalid audience"}""", HttpStatusCode.Forbidden);
    await using var host = await McpTestHost.StartAsync(api, agentApi);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_accounts_summary", new { year = 2026, month = 11 });

    Assert.True(IsError(response));
    Assert.Contains("trackItsValue refused this request: Invalid audience", ResultText(response));
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

  [Fact]
  public async Task A_list_of_transactions_arrives_as_objects_and_is_applied_and_saved_once()
  {
    const string edited = """{"id":"b-1","isDirty":true}""";
    var api = new FakeTivApi(request =>
      request.Method == HttpMethod.Put ? FakeTivApi.Json(edited) : FakeTivApi.Json(request.RequestUri!.AbsolutePath.EndsWith("/budgeting/templates") ? "[]" : """{"id":"b-1","isNew":false}""")
    );
    string? sentToAgent = null;
    var agentApi = new FakeTivApi(request =>
    {
      sentToAgent = request.Content!.ReadAsStringAsync().Result;
      return FakeTivApi.Json("{\"success\":true,\"changes\":[{\"description\":\"Added two\"}],\"budget\":" + edited + "}");
    });
    await using var host = await McpTestHost.StartAsync(api, agentApi);

    var response = await CallTool(
      host,
      McpTestHost.CreateToken(),
      "add_actuals",
      new
      {
        year = 2026,
        month = 11,
        actuals = new object[]
        {
          new
          {
            itemDescription = "Groceries",
            amount = 55.90,
            description = "Aldi",
            date = "2026-11-22",
          },
          new
          {
            itemDescription = "Restaurants",
            amount = 59,
            description = "Parks Pizza",
            date = "2026-11-22",
            isCreditWithdrawl = true,
          },
        },
      }
    );

    Assert.False(IsError(response));
    Assert.Equal("Saved. Added two", ResultText(response));
    Assert.Equal($"{McpTestHost.TivAgentApiBaseUrl}/budget-agent/apply-tools", agentApi.SingleRequest.RequestUri!.AbsoluteUri);
    var calls = JsonDocument.Parse(sentToAgent!).RootElement.GetProperty("calls");
    Assert.Equal(2, calls.GetArrayLength());
    Assert.Equal("Aldi", calls[0].GetProperty("arguments").GetProperty("description").GetString());
    Assert.False(calls[0].GetProperty("arguments").GetProperty("is_credit_withdrawl").GetBoolean());
    Assert.True(calls[1].GetProperty("arguments").GetProperty("is_credit_withdrawl").GetBoolean());
    Assert.Single(api.Requests, request => request.Method == HttpMethod.Put);
  }

  [Fact]
  public async Task Creating_a_budget_copies_the_month_before_as_the_signed_in_user()
  {
    string? sent = null;
    var api = new FakeTivApi(request =>
    {
      if (request.Method == HttpMethod.Post)
      {
        sent = request.Content!.ReadAsStringAsync().Result;
        return FakeTivApi.Json("""{"id":"b-2","isNew":false}""");
      }
      return FakeTivApi.Json("""{"id":"b-1","isNew":false}""");
    });
    await using var host = await McpTestHost.StartAsync(api);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(host, token, "create_budget", new { year = 2026, month = 12 });

    Assert.False(IsError(response));
    Assert.StartsWith("Created the budget for 2026-12 as a copy of 2026-11", ResultText(response));
    Assert.Equal(
      [$"{McpTestHost.TivApiBaseUrl}/budgeting/budgets/default/2026/11", $"{McpTestHost.TivApiBaseUrl}/budgeting/budgets/copy"],
      api.Requests.Select(request => request.RequestUri!.AbsoluteUri)
    );
    Assert.Equal("b-1", JsonDocument.Parse(sent!).RootElement.GetProperty("sourceId").GetString());
    Assert.All(api.Requests, request => Assert.Equal(token, request.Headers.Authorization!.Parameter));
  }

  [Fact]
  public async Task Verify_account_transactions_posts_to_the_api_as_the_user_and_returns_its_answer()
  {
    string? sent = null;
    var api = new FakeTivApi(request =>
    {
      sent = request.Content!.ReadAsStringAsync().Result;
      return FakeTivApi.Json("""{"accountName":"James Discover Card","markedCount":12,"remainingInEarlierMonthsCount":3}""");
    });
    await using var host = await McpTestHost.StartAsync(api);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(
      host,
      token,
      "verify_account_transactions",
      new
      {
        account = "James Discover Card",
        year = 2026,
        month = 10,
        throughDay = 25,
      }
    );

    Assert.False(IsError(response));
    Assert.Contains("\"markedCount\":12", ResultText(response));
    var request = Assert.Single(api.Requests);
    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal($"{McpTestHost.TivApiBaseUrl}/accounting/accounts/verify-transactions", request.RequestUri!.AbsoluteUri);
    Assert.Equal(token, request.Headers.Authorization!.Parameter);
    Assert.Equal("""{"accountName":"James Discover Card","year":2026,"month":10,"throughDay":25,"verified":true,"dryRun":false}""", sent);
  }

  [Theory]
  [InlineData(HttpStatusCode.Conflict, "Account month 2026-10 is closed. Closed months are read-only: reopen it before unverifying its transactions.")]
  [InlineData(HttpStatusCode.NotFound, "No account is named 'Discover'. The accounts are: 'James Discover Card', 'Checking'.")]
  [InlineData(HttpStatusCode.BadRequest, "2 accounts are named 'Checking'. Give the id of the one meant: a, b.")]
  public async Task Verify_account_transactions_passes_on_the_apis_refusal(HttpStatusCode status, string reason)
  {
    var body = JsonSerializer.Serialize(new { StatusCode = (int)status, Message = reason });
    var api = new FakeTivApi(_ => FakeTivApi.Json(body, status));
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(
      host,
      McpTestHost.CreateToken(),
      "verify_account_transactions",
      new
      {
        account = "Discover",
        year = 2026,
        month = 10,
        verified = false,
      }
    );

    Assert.True(IsError(response));
    Assert.Contains(reason, ResultText(response));
  }

  [Fact]
  public async Task When_the_month_already_has_a_budget_the_user_is_told_so()
  {
    var api = new FakeTivApi(request =>
      request.Method == HttpMethod.Post ? FakeTivApi.Json("\"A budget named 'default' already exists for 2026-12.\"", HttpStatusCode.Conflict) : FakeTivApi.Json("""{"id":"b-1","isNew":false}""")
    );
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "create_budget", new { year = 2026, month = 12 });

    Assert.True(IsError(response));
    Assert.Contains("A budget named 'default' already exists for 2026-12.", ResultText(response));
  }

  [Fact]
  public async Task A_user_below_the_level_a_tool_needs_is_told_what_unlocks_it()
  {
    var api = FakeTivApi.Returning("{\"StatusCode\":403,\"Message\":\"This feature is unlocked at level 4 (price trending).\"}", HttpStatusCode.Forbidden);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_item_trends", new { itemIds = new[] { "11111111-1111-1111-1111-111111111111" } });

    Assert.True(IsError(response));
    Assert.EndsWith("trackItsValue refused this request: This feature is unlocked at level 4 (price trending).", ResultText(response));
    Assert.Equal("?continuanceIds=11111111-1111-1111-1111-111111111111", api.SingleRequest.RequestUri!.Query);
  }

  [Fact]
  public async Task A_save_that_faults_tells_the_assistant_the_step_the_service_and_one_reference_for_the_whole_call()
  {
    var api = new FakeTivApi(request =>
      request.Method == HttpMethod.Put
        ? FakeTivApi.Json("{\"StatusCode\":500,\"Message\":\"Internal Server Error\"}", HttpStatusCode.InternalServerError)
        : FakeTivApi.Json(request.RequestUri!.AbsolutePath.EndsWith("/budgeting/templates") ? "[]" : "{\"id\":\"b-1\",\"isNew\":false}")
    );
    var agentApi = FakeTivApi.Returning("{\"success\":true,\"changes\":[],\"budget\":{\"id\":\"b-1\"}}");
    await using var host = await McpTestHost.StartAsync(api, agentApi);

    var response = await CallTool(
      host,
      McpTestHost.CreateToken(),
      "add_actual",
      new
      {
        year = 2026,
        month = 9,
        itemDescription = "Restaurants",
        amount = 28.17,
        description = "Taco Bell",
        date = "2026-09-22",
      }
    );

    Assert.True(IsError(response));
    var references = api.Requests.Concat(agentApi.Requests).Select(request => request.Headers.GetValues("X-Request-Id").Single()).Distinct().ToList();
    var reference = Assert.Single(references);
    Assert.Matches("^[0-9a-f]{8}$", reference);
    Assert.EndsWith(
      $"Saving the budget failed. trackItsValue ran into a problem of its own (status 500). Try again later. Reference {reference} (budget API). "
        + "The save may or may not have gone through. Read the budget again before retrying, so nothing is added twice.",
      ResultText(response)
    );
  }

  [Fact]
  public async Task Two_tool_calls_get_two_references()
  {
    var api = FakeTivApi.Returning("[]");
    await using var host = await McpTestHost.StartAsync(api);
    var token = McpTestHost.CreateToken();

    await CallTool(host, token, "list_accounts", new { });
    await CallTool(host, token, "list_accounts", new { });

    Assert.Equal(2, api.Requests.Select(request => request.Headers.GetValues("X-Request-Id").Single()).Distinct().Count());
  }

  [Fact]
  public async Task An_assistant_is_told_on_connecting_how_to_read_a_failure()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(
      host,
      McpTestHost.CreateToken(),
      "initialize",
      new
      {
        protocolVersion = "2025-06-18",
        capabilities = new { },
        clientInfo = new { name = "test", version = "1" },
      }
    );

    var instructions = response.GetProperty("result").GetProperty("instructions").GetString();
    Assert.Contains("'Nothing was saved' means the budget is unchanged", instructions);
    Assert.Contains("'The save may or may not have gone through' means the outcome is unknown", instructions);
    Assert.Contains("tell the user that reference exactly", instructions);
    Assert.Contains("use add_actuals, not add_actual repeatedly", instructions);
    Assert.Contains("create_budget makes one", instructions);
  }

  [Fact]
  public async Task Every_tool_that_saves_a_budget_edit_says_what_to_do_when_the_save_is_in_doubt()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(host, McpTestHost.CreateToken(), "tools/list");

    var descriptions = response
      .GetProperty("result")
      .GetProperty("tools")
      .EnumerateArray()
      .ToDictionary(tool => tool.GetProperty("name").GetString()!, tool => tool.GetProperty("description").GetString()!);
    // create_budget saves through a different call, which either makes the whole budget or nothing.
    // verify_account_transactions saves no budget, and says itself that repeating it is safe.
    Assert.All(WriteTools.Except(["create_budget", "verify_account_transactions"]), name => Assert.Contains("check with get_budget_summary before retrying", descriptions[name]));
    Assert.Contains("use add_actuals instead", descriptions["add_actual"]);
    Assert.Contains("Repeating a call is safe", descriptions["verify_account_transactions"]);
  }

  [Fact]
  public async Task Every_tool_the_instructions_name_exists()
  {
    await using var host = await McpTestHost.StartAsync();

    var response = await Rpc(host, McpTestHost.CreateToken(), "tools/list");

    var names = response.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).ToHashSet();
    var named = System.Text.RegularExpressions.Regex.Matches(tivMcp.Api.Constants.ServerInstructions.Text, "\\b[a-z]+(_[a-z]+)+\\b").Select(match => match.Value).Distinct().ToList();
    Assert.NotEmpty(named);
    Assert.All(named, name => Assert.Contains(name, names));
  }

  // ---- The monthly tool call allowance ----

  private const string AllowanceUsedUp = """{"allowed":false,"tier":"free","resetAtUtc":"2026-12-01T00:00:00Z","dimensions":[{"name":"calls","used":2000,"limit":2000}]}""";

  [Fact]
  public async Task A_tool_call_is_checked_against_the_allowance_first_and_counted_after_as_the_same_user()
  {
    var api = FakeTivApi.Returning("[]");
    await using var host = await McpTestHost.StartAsync(api);
    var token = McpTestHost.CreateToken();

    var response = await CallTool(host, token, "list_accounts", new { });

    Assert.False(IsError(response));
    Assert.Equal(
      [(HttpMethod.Get, $"{McpTestHost.TivApiBaseUrl}/usage/check?bucket=mcp-tool"), (HttpMethod.Post, $"{McpTestHost.TivApiBaseUrl}/usage/record")],
      api.UsageRequests.Select(request => (request.Method, request.RequestUri!.AbsoluteUri))
    );
    Assert.All(api.UsageRequests, request => Assert.Equal(token, request.Headers.Authorization!.Parameter));
    Assert.Single(api.Requests);
  }

  [Fact]
  public async Task When_the_allowance_is_used_up_the_tool_does_not_run_and_the_assistant_is_told_why()
  {
    var api = FakeTivApi.Returning("[]");
    api.RespondToUsage = _ => FakeTivApi.Json(AllowanceUsedUp);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "list_accounts", new { });

    Assert.True(IsError(response));
    Assert.Equal(
      "This month's assistant tool calls are used up: 2,000 are included in the free plan. The count starts again on December 1, 2026 (UTC). "
        + "Nothing was done, and retrying before then will not help. A paid plan includes more.",
      ResultText(response)
    );
    Assert.Empty(api.Requests);
    Assert.Equal(HttpMethod.Get, Assert.Single(api.UsageRequests).Method);
  }

  [Fact]
  public async Task A_tool_call_that_fails_is_not_counted()
  {
    var api = FakeTivApi.Returning("{}", HttpStatusCode.Forbidden);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "list_accounts", new { });

    Assert.True(IsError(response));
    Assert.DoesNotContain(api.UsageRequests, request => request.Method == HttpMethod.Post);
  }

  [Fact]
  public async Task A_bad_argument_is_not_counted()
  {
    var api = FakeTivApi.Returning("{}");
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_accounts_overview", new { year = 2026, month = 13 });

    Assert.True(IsError(response));
    Assert.DoesNotContain(api.UsageRequests, request => request.Method == HttpMethod.Post);
  }

  [Fact]
  public async Task When_the_allowance_cannot_be_checked_the_tool_still_runs()
  {
    var api = FakeTivApi.Returning("[]");
    api.RespondToUsage = _ => FakeTivApi.Json("{}", HttpStatusCode.ServiceUnavailable);
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "list_accounts", new { });

    Assert.False(IsError(response));
    Assert.Equal("[]", ResultText(response));
  }

  [Fact]
  public async Task Confirming_the_connection_is_not_counted_and_asks_the_api_nothing()
  {
    var api = FakeTivApi.Returning("{}");
    await using var host = await McpTestHost.StartAsync(api);

    var response = await CallTool(host, McpTestHost.CreateToken(), "get_server_info", new { });

    Assert.False(IsError(response));
    Assert.Empty(api.UsageRequests);
    Assert.Empty(api.Requests);
  }
}
