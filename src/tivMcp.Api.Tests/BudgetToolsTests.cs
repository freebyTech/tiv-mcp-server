using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

public class BudgetToolsTests
{
  private const string BudgetJson = """{"description":"default","year":2026,"month":3,"budgetCategories":[{"description":"Food","budgetItems":[]}]}""";

  private readonly FakeTivApiClient _api = new();

  private BudgetTools Tools => new(_api);

  [Fact]
  public async Task ListBudgets_asks_for_the_requested_number()
  {
    _api.JsonToReturn = "[]";

    var result = await Tools.ListBudgets(24);

    Assert.Equal("budgeting/budgets/list/24", Assert.Single(_api.Calls).Path);
    Assert.Equal("[]", result);
  }

  [Fact]
  public async Task ListBudgets_defaults_to_twelve()
  {
    await Tools.ListBudgets();

    Assert.Equal("budgeting/budgets/list/12", Assert.Single(_api.Calls).Path);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(121)]
  public async Task ListBudgets_rejects_a_count_out_of_range_without_calling_the_api(int count)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.ListBudgets(count));

    Assert.Contains("count must be between 1 and 120", error.Message);
    Assert.Empty(_api.Calls);
  }

  [Fact]
  public async Task GetBudget_asks_for_the_main_budget_by_default()
  {
    _api.JsonToReturn = BudgetJson;

    var result = await Tools.GetBudget(2026, 3);

    Assert.Equal("budgeting/budgets/default/2026/3", Assert.Single(_api.Calls).Path);
    Assert.Equal(BudgetJson, result);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task GetBudget_treats_a_blank_name_as_the_main_budget(string budgetName)
  {
    await Tools.GetBudget(2026, 3, budgetName);

    Assert.Equal("budgeting/budgets/default/2026/3", Assert.Single(_api.Calls).Path);
  }

  [Fact]
  public async Task GetBudget_escapes_a_named_budget_into_one_path_segment()
  {
    await Tools.GetBudget(2026, 12, " Rental house #2? ");

    Assert.Equal("budgeting/budgets/Rental%20house%20%232%3F/2026/12", Assert.Single(_api.Calls).Path);
  }

  [Theory]
  [InlineData("a/b")]
  [InlineData("..\\secrets")]
  public async Task GetBudget_rejects_a_name_that_could_change_the_path(string budgetName)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudget(2026, 3, budgetName));

    Assert.Contains("budgetName", error.Message);
    Assert.Empty(_api.Calls);
  }

  [Theory]
  [InlineData(1999, 6, "year")]
  [InlineData(2101, 6, "year")]
  [InlineData(2026, 0, "month")]
  [InlineData(2026, 13, "month")]
  public async Task GetBudget_rejects_an_impossible_year_or_month_without_calling_the_api(int year, int month, string named)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetBudget(year, month));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Calls);
  }
}
