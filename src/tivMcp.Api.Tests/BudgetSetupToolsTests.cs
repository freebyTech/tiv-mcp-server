using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

/// <summary>
/// create_budget gets the budget to copy from for its id, then asks the API to copy it into the
/// new month without its transactions. Whether the month may be created is the API's decision.
/// </summary>
public class BudgetSetupToolsTests
{
  private const string November = """{"id":"11111111-1111-1111-1111-111111111111","isNew":false,"budgetCategories":[]}""";

  private readonly FakeTivApiClient _api = new() { JsonToReturn = November };

  private BudgetSetupTools Tools => new(_api);

  private JsonElement Copy
  {
    get
    {
      var post = Assert.Single(_api.Posts);
      Assert.Equal("budgeting/budgets/copy", post.Path);
      return JsonDocument.Parse(post.Body).RootElement;
    }
  }

  [Fact]
  public async Task Copies_the_month_before_by_default_without_its_transactions()
  {
    await Tools.CreateBudget(2026, 12);

    Assert.Equal("budgeting/budgets/default/2026/11", Assert.Single(_api.Calls).Path);
    Assert.Equal(
      """{"sourceId":"11111111-1111-1111-1111-111111111111","destinationDescription":"default","destinationMonth":12,"destinationYear":2026,"copyActuals":false}""",
      Assert.Single(_api.Posts).Body
    );
  }

  [Fact]
  public async Task The_month_before_january_is_december_of_the_year_before()
  {
    await Tools.CreateBudget(2027, 1);

    Assert.Equal("budgeting/budgets/default/2026/12", Assert.Single(_api.Calls).Path);
    Assert.Equal(2027, Copy.GetProperty("destinationYear").GetInt32());
    Assert.Equal(1, Copy.GetProperty("destinationMonth").GetInt32());
  }

  [Fact]
  public async Task Can_copy_a_month_other_than_the_one_before()
  {
    await Tools.CreateBudget(2026, 12, copyFromYear: 2025, copyFromMonth: 12);

    Assert.Equal("budgeting/budgets/default/2025/12", Assert.Single(_api.Calls).Path);
  }

  [Fact]
  public async Task A_named_budget_is_copied_from_and_to_that_name()
  {
    await Tools.CreateBudget(2026, 12, " Rental house #2 ");

    Assert.Equal("budgeting/budgets/Rental%20house%20%232/2026/11", Assert.Single(_api.Calls).Path);
    Assert.Equal("Rental house #2", Copy.GetProperty("destinationDescription").GetString());
  }

  [Fact]
  public async Task Says_what_was_created_and_does_not_pass_on_the_whole_new_budget()
  {
    _api.JsonByPath["budgeting/budgets/copy"] = """{"id":"new","budgetCategories":[{"description":"A very long budget"}]}""";

    var result = await Tools.CreateBudget(2026, 12);

    Assert.StartsWith("Created the budget for 2026-12 as a copy of 2026-11", result);
    Assert.DoesNotContain("A very long budget", result);
  }

  [Theory]
  [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","isNew":true}""")]
  [InlineData("""{"isNew":false}""")]
  [InlineData("")]
  [InlineData("not json")]
  [InlineData("[]")]
  public async Task Nothing_is_created_when_there_is_no_budget_to_copy_from(string source)
  {
    _api.JsonToReturn = source;

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudget(2026, 12));

    Assert.StartsWith("There is no budget for 2026-11 to copy from.", error.Message);
    Assert.Contains("list_budgets", error.Message);
    Assert.Empty(_api.Posts);
  }

  [Theory]
  [InlineData(2026, null)]
  [InlineData(null, 11)]
  public async Task The_month_to_copy_from_needs_both_its_year_and_month(int? copyFromYear, int? copyFromMonth)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudget(2026, 12, copyFromYear: copyFromYear, copyFromMonth: copyFromMonth));

    Assert.StartsWith("copyFromYear and copyFromMonth must be given together", error.Message);
    Assert.Empty(_api.Calls);
  }

  [Theory]
  [InlineData(1999, 6, null, null, null, "year")]
  [InlineData(2026, 13, null, null, null, "month")]
  [InlineData(2026, 6, "a/b", null, null, "budgetName")]
  [InlineData(2026, 6, null, 1999, 5, "copyFromYear")]
  [InlineData(2026, 6, null, 2026, 0, "copyFromMonth")]
  public async Task A_bad_argument_stops_before_any_call(int year, int month, string? budgetName, int? copyFromYear, int? copyFromMonth, string named)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudget(year, month, budgetName, copyFromYear, copyFromMonth));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Calls);
    Assert.Empty(_api.Posts);
  }

  [Fact]
  public async Task A_month_cannot_be_a_copy_of_itself()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudget(2026, 12, copyFromYear: 2026, copyFromMonth: 12));

    Assert.StartsWith("A budget cannot be created as a copy of its own month.", error.Message);
    Assert.Empty(_api.Calls);
  }

  // ---- From the wizard's answers ----

  [Fact]
  public async Task CreateBudgetFromAnswers_posts_the_answers_under_the_apis_names_for_a_month_with_no_budget()
  {
    _api.JsonByPath["budgeting/budgets/default/2026/12"] = """{"id":"b-new","isNew":true}""";
    var answers = new Models.BudgetQuestionAnswers
    {
      ReceivePaychecks = true,
      PaycheckCount = 2,
      PaycheckAmount = 2819.03m,
      PayRent = true,
      HousingAmount = 1450m,
    };

    var answer = await Tools.CreateBudgetFromAnswers(2026, 12, answers);

    var post = Assert.Single(_api.Posts);
    Assert.Equal("budgeting/budgets/createNewBudget", post.Path);
    var sent = System.Text.Json.JsonDocument.Parse(post.Body).RootElement;
    Assert.Equal(("default", 2026, 12), (sent.GetProperty("description").GetString(), sent.GetProperty("year").GetInt32(), sent.GetProperty("month").GetInt32()));
    var questions = sent.GetProperty("budgetWizardQuestion");
    Assert.True(questions.GetProperty("receivePaychecks").GetBoolean());
    Assert.Equal(2, questions.GetProperty("paycheckCount").GetInt32());
    Assert.Equal(2819.03m, questions.GetProperty("paycheckAmount").GetDecimal());
    Assert.Equal(1450m, questions.GetProperty("housingAmount").GetDecimal());
    // Every yes/no is sent as a real true or false, which the API insists on.
    Assert.Equal(System.Text.Json.JsonValueKind.False, questions.GetProperty("payMortgage").ValueKind);
    Assert.Equal(System.Text.Json.JsonValueKind.Null, questions.GetProperty("groceriesAmount").ValueKind);
    Assert.StartsWith("Created the budget for 2026-12 from the answers.", answer);
  }

  [Fact]
  public async Task CreateBudgetFromAnswers_leaves_a_month_that_has_a_budget_alone()
  {
    _api.JsonByPath["budgeting/budgets/default/2026/12"] = """{"id":"b-1","isNew":false}""";

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudgetFromAnswers(2026, 12, new Models.BudgetQuestionAnswers()));

    Assert.StartsWith("There is already a budget for 2026-12.", error.Message);
    Assert.Empty(_api.Posts);
  }

  [Fact]
  public async Task CreateBudgetFromAnswers_needs_the_answers_and_a_real_month()
  {
    await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudgetFromAnswers(2026, 12, null!));
    await Assert.ThrowsAsync<McpException>(() => Tools.CreateBudgetFromAnswers(2026, 13, new Models.BudgetQuestionAnswers()));

    Assert.Empty(_api.Calls);
  }
}
