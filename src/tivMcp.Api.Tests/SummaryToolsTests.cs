using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

public class SummaryToolsTests
{
  private const string BudgetJson = """{"description":"default","year":2026,"month":11,"budgetCategories":[{"description":"Food","budgetItems":[]}]}""";
  private const string AccountsJson = """{"year":2026,"month":11,"accountTypes":[]}""";

  private readonly FakeTivApiClient _api = new();
  private readonly FakeTivAgentApiClient _agentApi = new();

  private SummaryTools Tools => new(_api, _agentApi);

  [Fact]
  public async Task GetBudgetSummary_gets_the_budget_and_hands_it_unchanged_to_the_agent_api()
  {
    _api.JsonToReturn = BudgetJson;

    await Tools.GetBudgetSummary(2026, 11);

    Assert.Equal("budgeting/budgets/default/2026/11", Assert.Single(_api.Calls).Path);
    var agentCall = Assert.Single(_agentApi.Calls);
    Assert.Equal("budget-agent/summarize-budget", agentCall.Path);
    Assert.Equal($$"""{"budget":{{BudgetJson}}}""", agentCall.Body);
  }

  [Fact]
  public async Task GetBudgetSummary_returns_the_agent_apis_lines_as_text()
  {
    _api.JsonToReturn = BudgetJson;
    _agentApi.JsonToReturn = """{"summary":["Budget: default 2026-11","","[Food] Budgeted=$700.00, Spent=$0.00"]}""";

    var result = await Tools.GetBudgetSummary(2026, 11);

    Assert.Equal("Budget: default 2026-11\n\n[Food] Budgeted=$700.00, Spent=$0.00", result);
  }

  [Fact]
  public async Task GetBudgetSummary_can_send_the_budget_templates_along_unchanged()
  {
    const string templates = """[{"id":"t-1","description":"Food & Essentials"}]""";
    _api.JsonByPath["budgeting/budgets/default/2026/11"] = BudgetJson;
    _api.JsonByPath["budgeting/templates"] = templates;

    await Tools.GetBudgetSummary(2026, 11, includeTemplates: true);

    Assert.Equal(["budgeting/budgets/default/2026/11", "budgeting/templates"], _api.Calls.Select(call => call.Path));
    Assert.Equal("{\"budget\":" + BudgetJson + ",\"budget-templates\":" + templates + "}", Assert.Single(_agentApi.Calls).Body);
  }

  [Fact]
  public async Task GetBudgetSummary_asks_for_a_named_budget_as_one_path_segment()
  {
    _api.JsonToReturn = BudgetJson;

    await Tools.GetBudgetSummary(2026, 12, " Rental house #2? ");

    Assert.Equal("budgeting/budgets/Rental%20house%20%232%3F/2026/12", Assert.Single(_api.Calls).Path);
  }

  [Theory]
  [InlineData(1999, 6, "year")]
  [InlineData(2026, 13, "month")]
  public async Task GetBudgetSummary_rejects_an_impossible_year_or_month_without_calling_anything(int year, int month, string named)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudgetSummary(year, month));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Calls);
    Assert.Empty(_agentApi.Calls);
  }

  [Fact]
  public async Task GetBudgetSummary_rejects_a_name_that_could_change_the_path()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudgetSummary(2026, 3, "a/b"));

    Assert.Contains("budgetName", error.Message);
    Assert.Empty(_api.Calls);
  }

  [Fact]
  public async Task GetAccountsSummary_gets_the_overview_and_hands_it_unchanged_to_the_agent_api()
  {
    _api.JsonToReturn = AccountsJson;
    _agentApi.JsonToReturn = """{"summary":["Accounts Overview: 2026/11"]}""";

    var result = await Tools.GetAccountsSummary(2026, 11);

    Assert.Equal("accounting/accounts/all/2026/11", Assert.Single(_api.Calls).Path);
    var agentCall = Assert.Single(_agentApi.Calls);
    Assert.Equal("budget-agent/summarize-accounts", agentCall.Path);
    Assert.Equal($$"""{"accounts":{{AccountsJson}}}""", agentCall.Body);
    Assert.Equal("Accounts Overview: 2026/11", result);
  }

  [Theory]
  [InlineData(2101, 6, "year")]
  [InlineData(2026, 0, "month")]
  public async Task GetAccountsSummary_rejects_an_impossible_year_or_month_without_calling_anything(int year, int month, string named)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetAccountsSummary(year, month));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Calls);
    Assert.Empty(_agentApi.Calls);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task An_empty_answer_from_the_api_is_not_sent_on(string apiAnswer)
  {
    _api.JsonToReturn = apiAnswer;

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudgetSummary(2026, 11));

    Assert.Contains("nothing to summarize", error.Message);
    Assert.Empty(_agentApi.Calls);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not json")]
  [InlineData("{}")]
  [InlineData("""{"summary":null}""")]
  [InlineData("""{"summary":"one string"}""")]
  public async Task An_answer_from_the_agent_api_that_is_not_a_summary_is_a_clear_error(string agentAnswer)
  {
    _api.JsonToReturn = BudgetJson;
    _agentApi.JsonToReturn = agentAnswer;

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudgetSummary(2026, 11));

    Assert.Contains("could not read", error.Message);
  }
}
