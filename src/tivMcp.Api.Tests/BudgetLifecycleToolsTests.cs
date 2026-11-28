using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

/// <summary>
/// The budget lifecycle tools find the budget's id and make the one API call the website makes.
/// Whether a budget may be closed, reopened or predicted is the API's decision.
/// </summary>
public class BudgetLifecycleToolsTests
{
  private const string BudgetPath = "budgeting/budgets/default/2026/10";
  private const string BudgetJson = """{"id":"b-7", "description":"default","isNew":false,"isClosed":false,"budgetCategories":[]}""";

  private readonly FakeTivApiClient _api = new();

  public BudgetLifecycleToolsTests()
  {
    _api.JsonByPath[BudgetPath] = BudgetJson;
  }

  private BudgetLifecycleTools Tools => new(_api);

  [Fact]
  public async Task SetBudgetPrediction_posts_to_the_budget_by_id()
  {
    var answer = await Tools.SetBudgetPrediction(2026, 10);

    Assert.Equal(BudgetPath, Assert.Single(_api.Calls).Path);
    Assert.Equal("budgeting/budgets/b-7/set-prediction", Assert.Single(_api.Posts).Path);
    Assert.Equal("Set the prediction of the 2026-10 budget to what each category is budgeted now.", answer);
  }

  [Fact]
  public async Task CloseBudget_reports_the_points_the_api_awarded()
  {
    _api.JsonByPath["budgeting/budgets/b-7/close"] = """{"budget":{"id":"b-7"},"closedXp":50,"onTargetXp":75,"variancePct":2.5}""";

    var answer = await Tools.CloseBudget(2026, 10);

    Assert.Equal("budgeting/budgets/b-7/close", Assert.Single(_api.Posts).Path);
    Assert.Equal("Closed the 2026-10 budget. It earned 125 experience points, 75 of them for being on target.", answer);
  }

  [Fact]
  public async Task CloseBudget_passes_on_that_what_is_left_over_was_acknowledged()
  {
    _api.JsonByPath["budgeting/budgets/b-7/close?acknowledgeRemaining=true"] = """{"closedXp":0,"onTargetXp":0}""";

    var answer = await Tools.CloseBudget(2026, 10, acknowledgeRemaining: true);

    Assert.Equal("budgeting/budgets/b-7/close?acknowledgeRemaining=true", Assert.Single(_api.Posts).Path);
    Assert.Equal("Closed the 2026-10 budget. It earned 0 experience points.", answer);
  }

  [Fact]
  public async Task ReopenBudget_reports_the_points_taken_back()
  {
    _api.JsonByPath["budgeting/budgets/b-7/reopen"] = """{"budget":{"id":"b-7"},"rowsDeleted":12,"xpDeducted":125}""";

    var answer = await Tools.ReopenBudget(2026, 10);

    Assert.Equal("budgeting/budgets/b-7/reopen", Assert.Single(_api.Posts).Path);
    Assert.Equal("Reopened the 2026-10 budget. 125 experience points were taken back.", answer);
  }

  [Fact]
  public async Task DeleteBudget_sends_the_budget_it_read_as_the_website_does()
  {
    var answer = await Tools.DeleteBudget(2026, 10);

    var delete = Assert.Single(_api.Deletes);
    Assert.Equal("budgeting/budgets", delete.Path);
    Assert.Equal(BudgetJson, delete.Body);
    Assert.Equal("Deleted the 2026-10 budget with everything in it.", answer);
  }

  [Fact]
  public async Task DeleteBudget_leaves_whether_a_closed_budget_may_go_to_the_api()
  {
    const string closed = """{"id":"b-7","isNew":false,"isClosed":true}""";
    _api.JsonByPath[BudgetPath] = closed;

    await Tools.DeleteBudget(2026, 10);

    // The API refuses a closed budget with a 409; this server does not second-guess it.
    Assert.Equal(closed, Assert.Single(_api.Deletes).Body);
  }

  [Fact]
  public async Task A_named_budget_is_asked_for_as_one_path_segment()
  {
    _api.JsonByPath["budgeting/budgets/Rental%20%26%20Co/2026/10"] = BudgetJson;

    await Tools.ReopenBudget(2026, 10, " Rental & Co ");

    Assert.Equal("budgeting/budgets/Rental%20%26%20Co/2026/10", Assert.Single(_api.Calls).Path);
  }

  [Theory]
  [InlineData("""{"id":"b-7","isNew":true}""")]
  [InlineData("""{"isNew":false}""")]
  [InlineData("")]
  [InlineData("not json")]
  public async Task A_month_with_no_budget_stops_every_tool_before_it_changes_anything(string budget)
  {
    _api.JsonByPath[BudgetPath] = budget;

    var errors = new[]
    {
      await Assert.ThrowsAsync<McpException>(() => Tools.SetBudgetPrediction(2026, 10)),
      await Assert.ThrowsAsync<McpException>(() => Tools.CloseBudget(2026, 10)),
      await Assert.ThrowsAsync<McpException>(() => Tools.ReopenBudget(2026, 10)),
      await Assert.ThrowsAsync<McpException>(() => Tools.DeleteBudget(2026, 10)),
    };

    Assert.All(errors, error => Assert.StartsWith("There is no budget for 2026-10.", error.Message));
    Assert.Empty(_api.Posts);
    Assert.Empty(_api.Deletes);
  }

  [Fact]
  public async Task A_bad_month_stops_before_any_call()
  {
    await Assert.ThrowsAsync<McpException>(() => Tools.CloseBudget(2026, 0));
    await Assert.ThrowsAsync<McpException>(() => Tools.DeleteBudget(1999, 10));

    Assert.Empty(_api.Calls);
  }
}
