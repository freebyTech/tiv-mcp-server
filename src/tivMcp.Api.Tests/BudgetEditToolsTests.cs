using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

/// <summary>
/// Each edit tool must name the agent API tool it mirrors and pass that tool's arguments under the
/// agent API's own argument names. Arguments the caller left out are passed as null, which the
/// editor leaves out of the request.
/// </summary>
public class BudgetEditToolsTests
{
  private readonly FakeBudgetEditor _editor = new();

  private BudgetEditTools Tools => new(_editor);

  private void AssertCall(string tool, Dictionary<string, object?> arguments, string? budgetName = null, bool saves = true)
  {
    var call = _editor.SingleCall;
    Assert.Equal(2026, call.Year);
    Assert.Equal(11, call.Month);
    Assert.Equal(budgetName, call.BudgetName);
    Assert.Equal(tool, call.Tool);
    Assert.Equal(saves, call.Saves);
    Assert.Equal(arguments.OrderBy(a => a.Key), call.Arguments.OrderBy(a => a.Key));
  }

  [Fact]
  public async Task UpdateEstimates()
  {
    await Tools.UpdateEstimates(2026, 11, 5000m, 4200.50m);

    AssertCall("update_estimates", new() { ["estimated_income"] = 5000m, ["estimated_spending"] = 4200.50m });
  }

  [Fact]
  public async Task AddCategory()
  {
    await Tools.AddCategory(2026, 11, " Transportation ", "Auto & Transport", "Rental");

    AssertCall("add_category", new() { ["description"] = "Transportation", ["category_template_description"] = "Auto & Transport" }, "Rental");
  }

  [Fact]
  public async Task UpdateCategory()
  {
    await Tools.UpdateCategory(2026, 11, "Food", "Food & Drink");

    AssertCall("update_category", new() { ["category_description"] = "Food", ["new_description"] = "Food & Drink" });
  }

  [Fact]
  public async Task DeleteCategory()
  {
    await Tools.DeleteCategory(2026, 11, "Food");

    AssertCall("delete_category", new() { ["category_description"] = "Food" });
  }

  [Fact]
  public async Task AddItem()
  {
    await Tools.AddItem(2026, 11, "Food", "Coffee", "Restaurants", 40m);

    AssertCall(
      "add_item",
      new()
      {
        ["category_description"] = "Food",
        ["item_description"] = "Coffee",
        ["item_template_description"] = "Restaurants",
        ["amount_budgeted"] = 40m,
      }
    );
  }

  [Fact]
  public async Task UpdateItem_passes_only_what_was_given()
  {
    await Tools.UpdateItem(2026, 11, "Groceries", newAmountBudgeted: 450m);

    AssertCall(
      "update_item",
      new()
      {
        ["item_description"] = "Groceries",
        ["new_amount_budgeted"] = 450m,
        ["new_description"] = null,
        ["category_description"] = null,
      }
    );
  }

  [Fact]
  public async Task DeleteItem()
  {
    await Tools.DeleteItem(2026, 11, "Groceries", "Food");

    AssertCall("delete_item", new() { ["item_description"] = "Groceries", ["category_description"] = "Food" });
  }

  [Fact]
  public async Task AddActual_for_a_plain_purchase()
  {
    await Tools.AddActual(2026, 11, "Groceries", 55.90m, "Aldi", "2026-11-22");

    AssertCall(
      "add_actual",
      new()
      {
        ["item_description"] = "Groceries",
        ["amount"] = 55.90m,
        ["description"] = "Aldi",
        ["date"] = "2026-11-22",
        ["category_description"] = null,
        ["is_credit_withdrawl"] = false,
        ["account_link_id"] = null,
        ["account_category_link_id"] = null,
      }
    );
  }

  [Fact]
  public async Task AddActual_for_a_credit_card_purchase_on_a_named_account()
  {
    const string account = "11111111-1111-1111-1111-111111111111";
    const string category = "22222222-2222-2222-2222-222222222222";

    await Tools.AddActual(2026, 11, "Restaurants", 25m, "Chipotle", "2026-11-03", "Food", true, account, category);

    AssertCall(
      "add_actual",
      new()
      {
        ["item_description"] = "Restaurants",
        ["amount"] = 25m,
        ["description"] = "Chipotle",
        ["date"] = "2026-11-03",
        ["category_description"] = "Food",
        ["is_credit_withdrawl"] = true,
        ["account_link_id"] = account,
        ["account_category_link_id"] = category,
      }
    );
  }

  [Fact]
  public async Task UpdateActual()
  {
    await Tools.UpdateActual(2026, 11, "Groceries", "Aldi", newAmount: 60m, newDate: "2026-11-23", actualDate: "2026-11-22", actualAmount: 55.90m);

    AssertCall(
      "update_actual",
      new()
      {
        ["item_description"] = "Groceries",
        ["actual_description"] = "Aldi",
        ["new_amount"] = 60m,
        ["new_description"] = null,
        ["new_date"] = "2026-11-23",
        ["category_description"] = null,
        ["actual_date"] = "2026-11-22",
        ["actual_amount"] = 55.90m,
      }
    );
  }

  [Fact]
  public async Task DeleteActual()
  {
    await Tools.DeleteActual(2026, 11, "Groceries", "Aldi", actualAmount: 55.90m);

    AssertCall(
      "delete_actual",
      new()
      {
        ["item_description"] = "Groceries",
        ["actual_description"] = "Aldi",
        ["category_description"] = null,
        ["actual_date"] = null,
        ["actual_amount"] = 55.90m,
      }
    );
  }

  [Fact]
  public async Task MoveBudget()
  {
    await Tools.MoveBudget(2026, 11, "Restaurants", "Groceries", 50m);

    AssertCall(
      "move_budget",
      new()
      {
        ["from_item"] = "Restaurants",
        ["to_item"] = "Groceries",
        ["amount"] = 50m,
        ["from_category"] = null,
        ["to_category"] = null,
      }
    );
  }

  [Fact]
  public async Task RectifyBudget()
  {
    await Tools.RectifyBudget(2026, 11);

    AssertCall("rectify_budget", new());
  }

  [Fact]
  public async Task RectifyCategory()
  {
    await Tools.RectifyCategory(2026, 11, "Food");

    AssertCall("rectify_category", new() { ["category_description"] = "Food" });
  }

  [Fact]
  public async Task RectifyItem()
  {
    await Tools.RectifyItem(2026, 11, "Groceries");

    AssertCall("rectify_item", new() { ["item_description"] = "Groceries", ["category_description"] = null });
  }

  [Fact]
  public async Task GetAvailableAccounts_only_reads()
  {
    await Tools.GetAvailableAccounts(2026, 11);

    AssertCall("get_available_accounts", new(), saves: false);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(null)]
  public async Task A_missing_name_is_rejected_before_anything_is_done(string? itemDescription)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActual(2026, 11, itemDescription!, 5m, "Aldi", "2026-11-22"));

    Assert.Equal("itemDescription is required.", error.Message);
    Assert.Empty(_editor.Calls);
  }

  [Theory]
  [InlineData("22/11/2026")]
  [InlineData("2026-11-31")]
  [InlineData("yesterday")]
  [InlineData("")]
  public async Task A_date_that_is_not_a_real_YYYY_MM_DD_date_is_rejected_before_anything_is_done(string date)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActual(2026, 11, "Groceries", 5m, "Aldi", date));

    Assert.StartsWith("date must be a date written as YYYY-MM-DD", error.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task An_optional_date_is_checked_when_given()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.UpdateActual(2026, 11, "Groceries", "Aldi", newDate: "tomorrow"));

    Assert.StartsWith("newDate must be a date", error.Message);
    Assert.Empty(_editor.Calls);
  }
}
