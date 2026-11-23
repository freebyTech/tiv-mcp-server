using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The editor makes the calls for one budget change in order: get the budget, accounts and
/// templates from the API, have the agent API apply the tool, and save the edited budget through
/// the API. It must pass everything along unchanged and never save after a failure.
/// </summary>
public class BudgetEditorTests
{
  private const string BudgetPath = "budgeting/budgets/default/2026/11";
  private const string AccountsPath = "accounting/accounts/all/2026/11";
  private const string TemplatesPath = "budgeting/templates";

  // Odd spacing and number formatting, to show the JSON is passed along as written.
  private const string BudgetJson = """{"id":"b-1", "description":"default","isNew":false,"estimatedIncome":5000.00,"budgetCategories":[]}""";
  private const string AccountsJson = """{"accountTypes":[]}""";
  private const string TemplatesJson = """[{"id":"t-1","description":"Food & Essentials"}]""";
  private const string EditedBudgetJson = """{"id":"b-1", "isDirty":true,"estimatedIncome":5000.00,"budgetCategories":[{"isNew":true,"amount":55.90}]}""";

  private static readonly Dictionary<string, object?> Aldi = new()
  {
    ["item_description"] = "Groceries",
    ["amount"] = 55.90m,
    ["description"] = "Aldi",
    ["date"] = "2026-11-22",
    ["category_description"] = null,
    ["is_credit_withdrawl"] = false,
  };

  private readonly FakeTivApiClient _api = new();
  private readonly FakeTivAgentApiClient _agentApi = new();

  public BudgetEditorTests()
  {
    _api.JsonByPath[BudgetPath] = BudgetJson;
    _api.JsonByPath[AccountsPath] = AccountsJson;
    _api.JsonByPath[TemplatesPath] = TemplatesJson;
    _agentApi.JsonToReturn =
      """{"success":true,"error":null,"changes":[{"entity_type":"BudgetActual","description":"Added 'Aldi' ($55.90) to 'Groceries'"}],"budget":""" + EditedBudgetJson + ""","data":{}}""";
  }

  private BudgetEditor Editor => new(_api, _agentApi);

  private JsonElement SentToAgent => JsonDocument.Parse(Assert.Single(_agentApi.Calls).Body).RootElement;

  [Fact]
  public async Task Gets_the_budget_the_months_accounts_and_the_templates()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    Assert.Equal([AccountsPath, BudgetPath, TemplatesPath], _api.Calls.Select(call => call.Path).Order());
  }

  [Fact]
  public async Task Asks_for_a_named_budget_as_one_path_segment()
  {
    _api.JsonToReturn = BudgetJson;

    await Editor.ApplyAndSaveAsync(2026, 11, " Rental house #2 ", "add_actual", Aldi);

    Assert.Contains(_api.Calls, call => call.Path == "budgeting/budgets/Rental%20house%20%232/2026/11");
  }

  [Fact]
  public async Task Sends_the_tool_its_arguments_and_the_apis_json_to_the_agent_api()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    var call = Assert.Single(_agentApi.Calls);
    Assert.Equal("budget-agent/apply-tool", call.Path);
    Assert.Equal(
      """{"tool":"add_actual","arguments":{"item_description":"Groceries","amount":55.90,"description":"Aldi","date":"2026-11-22","is_credit_withdrawl":false},"""
        + "\"context\":{\"budget\":"
        + BudgetJson
        + ",\"account-overview\":"
        + AccountsJson
        + ",\"budget-templates\":"
        + TemplatesJson
        + "}}",
      call.Body
    );
  }

  [Fact]
  public async Task Leaves_out_arguments_that_were_not_given()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    Assert.False(SentToAgent.GetProperty("arguments").TryGetProperty("category_description", out _));
  }

  [Fact]
  public async Task Text_arguments_cannot_break_out_of_the_request()
  {
    var arguments = new Dictionary<string, object?> { ["description"] = "He said \"hi\" \\ </script>\n" };

    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", arguments);

    Assert.Equal("He said \"hi\" \\ </script>\n", SentToAgent.GetProperty("arguments").GetProperty("description").GetString());
  }

  [Fact]
  public async Task Saves_the_edited_budget_exactly_as_the_agent_api_returned_it()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    var put = Assert.Single(_api.Puts);
    Assert.Equal("budgeting/budgets", put.Path);
    Assert.Equal(EditedBudgetJson, put.Body);
  }

  [Fact]
  public async Task Reports_what_was_changed()
  {
    var result = await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    Assert.Equal("Saved. Added 'Aldi' ($55.90) to 'Groceries'", result);
  }

  [Fact]
  public async Task Reports_a_save_even_when_the_tool_listed_no_changes()
  {
    _agentApi.JsonToReturn = $$"""{"success":true,"changes":[],"budget":{{EditedBudgetJson}}}""";

    Assert.Equal("Saved.", await Editor.ApplyAndSaveAsync(2026, 11, null, "rectify_budget", new Dictionary<string, object?>()));
  }

  [Theory]
  [InlineData("""{"id":"b-1","isNew":true,"budgetCategories":[]}""")]
  [InlineData("")]
  [InlineData("not json")]
  public async Task Does_nothing_when_there_is_no_budget_for_the_month(string budgetJson)
  {
    _api.JsonByPath[BudgetPath] = budgetJson;

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi));

    Assert.Equal("There is no budget for 2026-11 yet. Create it in trackItsValue first.", error.Message);
    Assert.Empty(_agentApi.Calls);
    Assert.Empty(_api.Puts);
  }

  [Fact]
  public async Task Does_not_save_when_the_tool_could_not_do_it_and_passes_on_its_reason()
  {
    _agentApi.JsonToReturn = """{"success":false,"error":"Item 'Yacht Fuel' not found","changes":[],"budget":null,"data":{}}""";

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi));

    Assert.Equal("Item 'Yacht Fuel' not found Nothing was saved.", error.Message);
    Assert.Empty(_api.Puts);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not json")]
  [InlineData("[]")]
  public async Task Does_not_save_when_the_agent_apis_answer_cannot_be_read(string answer)
  {
    _agentApi.JsonToReturn = answer;

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi));

    Assert.Contains("Nothing was saved", error.Message);
    Assert.Empty(_api.Puts);
  }

  [Theory]
  [InlineData("""{"success":true,"changes":[]}""")]
  [InlineData("""{"success":true,"changes":[],"budget":null}""")]
  [InlineData("""{"success":true,"changes":[],"budget":"oops"}""")]
  public async Task Does_not_save_when_a_success_carries_no_budget(string answer)
  {
    _agentApi.JsonToReturn = answer;

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi));

    Assert.Contains("Nothing was saved", error.Message);
    Assert.Empty(_api.Puts);
  }

  [Theory]
  [InlineData("")]
  [InlineData("null")]
  [InlineData("\"unexpected\"")]
  public async Task Accounts_or_templates_that_are_not_in_the_expected_form_are_left_out(string unexpected)
  {
    _api.JsonByPath[AccountsPath] = unexpected;
    _api.JsonByPath[TemplatesPath] = unexpected;

    await Editor.ApplyAndSaveAsync(2026, 11, null, "add_actual", Aldi);

    var context = SentToAgent.GetProperty("context");
    Assert.False(context.TryGetProperty("account-overview", out _));
    Assert.False(context.TryGetProperty("budget-templates", out _));
    Assert.Single(_api.Puts);
  }

  [Theory]
  [InlineData(1999, 6, null, "year")]
  [InlineData(2026, 13, null, "month")]
  [InlineData(2026, 6, "a/b", "budgetName")]
  public async Task A_bad_year_month_or_budget_name_stops_before_any_call(int year, int month, string? budgetName, string named)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(year, month, budgetName, "add_actual", Aldi));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Calls);
    Assert.Empty(_agentApi.Calls);
  }

  [Fact]
  public async Task Query_returns_the_tools_data_unchanged_and_saves_nothing()
  {
    const string data = """{"accounts":[{"id":"a-1","description":"Discover"}],"hint":"Use account 'id'"}""";
    _agentApi.JsonToReturn = $$"""{"success":true,"changes":[],"budget":{{BudgetJson}},"data":{{data}}}""";

    var result = await Editor.QueryAsync(2026, 11, null, "get_available_accounts");

    Assert.Equal(data, result);
    Assert.Equal("get_available_accounts", SentToAgent.GetProperty("tool").GetString());
    Assert.Empty(_api.Puts);
  }

  [Fact]
  public async Task Query_passes_on_the_tools_reason_when_it_fails()
  {
    _agentApi.JsonToReturn = """{"success":false,"error":"No account data available.","changes":[]}""";

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.QueryAsync(2026, 11, null, "get_available_accounts"));

    Assert.StartsWith("No account data available.", error.Message);
  }
}
