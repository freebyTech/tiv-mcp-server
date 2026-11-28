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
        ["is_envelope_deposit"] = null,
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
        ["is_envelope_deposit"] = null,
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

  private static Models.NewActual Entry(string item, decimal amount, string description, string date = "2026-11-22") =>
    new()
    {
      ItemDescription = item,
      Amount = amount,
      Description = description,
      Date = date,
    };

  [Fact]
  public async Task AddActuals_runs_add_actual_once_for_each_entry_in_order_on_the_one_budget()
  {
    var card = Entry(" Restaurants ", 59m, "Parks Pizza");
    card.IsCreditWithdrawl = true;
    card.CategoryDescription = "Food";

    await Tools.AddActuals(2026, 11, [Entry("Groceries", 55.90m, "Aldi"), card], "Rental");

    Assert.Empty(_editor.Calls);
    var batch = Assert.Single(_editor.Batches);
    Assert.Equal((2026, 11, "Rental"), (batch.Year, batch.Month, batch.BudgetName));
    Assert.All(batch.Calls, call => Assert.Equal("add_actual", call.Tool));
    Assert.Equal(["Aldi", "Parks Pizza"], batch.Calls.Select(call => call.Arguments["description"]));
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["item_description"] = "Restaurants",
        ["amount"] = 59m,
        ["description"] = "Parks Pizza",
        ["date"] = "2026-11-22",
        ["category_description"] = "Food",
        ["is_credit_withdrawl"] = true,
        ["account_link_id"] = null,
        ["account_category_link_id"] = null,
        ["is_envelope_deposit"] = null,
      }.OrderBy(a => a.Key),
      batch.Calls[1].Arguments.OrderBy(a => a.Key)
    );
  }

  [Fact]
  public async Task AddActuals_names_the_entry_that_is_wrong_and_records_none()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, [Entry("Groceries", 5m, "Aldi"), Entry("Gas", 40m, "Shell", "22/11/2026")]));

    Assert.StartsWith("actuals entry 2: date must be a date written as YYYY-MM-DD", error.Message);
    Assert.Empty(_editor.Batches);
  }

  [Fact]
  public async Task AddActuals_rejects_an_entry_without_an_item()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, [Entry(" ", 5m, "Aldi")]));

    Assert.Equal("actuals entry 1: itemDescription is required.", error.Message);
  }

  [Fact]
  public async Task AddActuals_rejects_an_empty_entry()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, [Entry("Groceries", 5m, "Aldi"), null!]));

    Assert.Equal("actuals entry 2 is empty.", error.Message);
    Assert.Empty(_editor.Batches);
  }

  [Fact]
  public async Task AddActuals_needs_at_least_one_entry()
  {
    Assert.StartsWith("actuals must list at least one", (await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, []))).Message);
    Assert.StartsWith("actuals must list at least one", (await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, null!))).Message);
    Assert.Empty(_editor.Batches);
  }

  [Fact]
  public async Task AddActuals_takes_fifty_entries_and_no_more()
  {
    await Tools.AddActuals(2026, 11, Enumerable.Range(0, 50).Select(_ => Entry("Groceries", 1m, "Aldi")).ToArray());
    Assert.Equal(50, Assert.Single(_editor.Batches).Calls.Count);

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, Enumerable.Range(0, 51).Select(_ => Entry("Groceries", 1m, "Aldi")).ToArray()));

    Assert.StartsWith("actuals can list at most 50 transactions, but 51 were given.", error.Message);
    Assert.Single(_editor.Batches);
  }

  // ---- Combined transactions ----

  private static Models.ActualReference Named(string item, string description, string? date = null, decimal? amount = null) =>
    new()
    {
      ItemDescription = item,
      ActualDescription = description,
      ActualDate = date,
      ActualAmount = amount,
    };

  private static List<Dictionary<string, object?>> ListArgument(FakeBudgetEditor.Call call, string name) => Assert.IsType<List<Dictionary<string, object?>>>(call.Arguments[name]);

  [Fact]
  public async Task AddActuals_combined_runs_one_tool_with_every_entry_so_they_share_a_combination()
  {
    var card = Entry(" Household ", 30m, "Costco");
    card.CategoryDescription = "Home";

    await Tools.AddActuals(2026, 11, [Entry("Groceries", 90m, "Costco"), card], "Rental", combined: true);

    Assert.Empty(_editor.Batches);
    var call = _editor.SingleCall;
    Assert.Equal((2026, 11, "Rental", "add_combined_actuals", true), (call.Year, call.Month, call.BudgetName, call.Tool, call.Saves));
    var entries = ListArgument(call, "actuals");
    Assert.Equal(2, entries.Count);
    // What the caller left out is left out of the entry, not sent as null.
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["item_description"] = "Household",
        ["amount"] = 30m,
        ["description"] = "Costco",
        ["date"] = "2026-11-22",
        ["category_description"] = "Home",
        ["is_credit_withdrawl"] = false,
      }.OrderBy(a => a.Key),
      entries[1].OrderBy(a => a.Key)
    );
  }

  [Fact]
  public async Task AddActuals_combined_needs_two_entries_and_checks_each_like_any_other()
  {
    var one = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, [Entry("Groceries", 90m, "Costco")], combined: true));
    var bad = await Assert.ThrowsAsync<McpException>(() => Tools.AddActuals(2026, 11, [Entry("Groceries", 90m, "Costco"), Entry("Household", 30m, " ")], combined: true));

    Assert.StartsWith("actuals must list at least 2 transactions when combined is true.", one.Message);
    Assert.Equal("actuals entry 2: description is required.", bad.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task SplitActual()
  {
    var lines = new[]
    {
      new Models.SplitLine
      {
        ItemDescription = " Medicine ",
        Amount = 12.34m,
        Description = "Walgreens pharmacy",
      },
      new Models.SplitLine
      {
        ItemDescription = "Household",
        Amount = 5m,
        CategoryDescription = "Home",
      },
    };

    await Tools.SplitActual(2026, 11, "Groceries", "Walgreens", lines, actualDate: "2026-11-22", actualAmount: 50m);

    var call = _editor.SingleCall;
    Assert.Equal(("split_actual", true), (call.Tool, call.Saves));
    Assert.Equal("Groceries", call.Arguments["item_description"]);
    Assert.Equal("Walgreens", call.Arguments["actual_description"]);
    Assert.Null(call.Arguments["category_description"]);
    Assert.Equal("2026-11-22", call.Arguments["actual_date"]);
    Assert.Equal(50m, call.Arguments["actual_amount"]);
    var sent = ListArgument(call, "splits");
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["item_description"] = "Medicine",
        ["amount"] = 12.34m,
        ["description"] = "Walgreens pharmacy",
      }.OrderBy(a => a.Key),
      sent[0].OrderBy(a => a.Key)
    );
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["item_description"] = "Household",
        ["amount"] = 5m,
        ["category_description"] = "Home",
      }.OrderBy(a => a.Key),
      sent[1].OrderBy(a => a.Key)
    );
  }

  [Fact]
  public async Task SplitActual_checks_its_lines_before_anything_is_sent()
  {
    var line = new Models.SplitLine { ItemDescription = "Medicine", Amount = 5m };

    var none = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", []));
    var missing = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", null!));
    var empty = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", [line, null!]));
    var noItem = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", [new Models.SplitLine { Amount = 5m }]));
    var tooMany = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", Enumerable.Repeat(line, 21).ToArray()));
    var badDate = await Assert.ThrowsAsync<McpException>(() => Tools.SplitActual(2026, 11, "Groceries", "Walgreens", [line], actualDate: "22/11/2026"));

    Assert.StartsWith("splits must list at least one part", none.Message);
    Assert.StartsWith("splits must list at least one part", missing.Message);
    Assert.Equal("splits entry 2 is empty.", empty.Message);
    Assert.Equal("splits entry 1: itemDescription is required.", noItem.Message);
    Assert.StartsWith("splits can list at most 20 parts, but 21 were given.", tooMany.Message);
    Assert.StartsWith("actualDate must be a date written as YYYY-MM-DD", badDate.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task CombineActuals()
  {
    await Tools.CombineActuals(2026, 11, [Named(" Groceries ", "Check 101", "2026-11-22", 60m), Named("Household", "Check 101")], "Rental");

    var call = _editor.SingleCall;
    Assert.Equal((2026, 11, "Rental", "combine_actuals", true), (call.Year, call.Month, call.BudgetName, call.Tool, call.Saves));
    var sent = ListArgument(call, "actuals");
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["item_description"] = "Groceries",
        ["actual_description"] = "Check 101",
        ["actual_date"] = "2026-11-22",
        ["actual_amount"] = 60m,
      }.OrderBy(a => a.Key),
      sent[0].OrderBy(a => a.Key)
    );
    Assert.Equal(new Dictionary<string, object?> { ["item_description"] = "Household", ["actual_description"] = "Check 101" }.OrderBy(a => a.Key), sent[1].OrderBy(a => a.Key));
  }

  [Fact]
  public async Task CombineActuals_needs_two_transactions_each_named()
  {
    var one = await Assert.ThrowsAsync<McpException>(() => Tools.CombineActuals(2026, 11, [Named("Groceries", "Check 101")]));
    var missing = await Assert.ThrowsAsync<McpException>(() => Tools.CombineActuals(2026, 11, null!));
    var unnamed = await Assert.ThrowsAsync<McpException>(() => Tools.CombineActuals(2026, 11, [Named("Groceries", "Check 101"), Named("Household", " ")]));
    var badDate = await Assert.ThrowsAsync<McpException>(() => Tools.CombineActuals(2026, 11, [Named("Groceries", "Check 101"), Named("Household", "Check 101", "Nov 22")]));
    var tooMany = await Assert.ThrowsAsync<McpException>(() => Tools.CombineActuals(2026, 11, Enumerable.Repeat(Named("Groceries", "Check 101"), 51).ToArray()));

    Assert.Equal("actuals must list at least 2 transactions.", one.Message);
    Assert.Equal("actuals must list at least 2 transactions.", missing.Message);
    Assert.Equal("actuals entry 2: actualDescription is required.", unnamed.Message);
    Assert.StartsWith("actuals entry 2: actualDate must be a date written as YYYY-MM-DD", badDate.Message);
    Assert.StartsWith("actuals can list at most 50 transactions, but 51 were given.", tooMany.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task SeparateActuals()
  {
    await Tools.SeparateActuals(2026, 11, [Named("Groceries", "Check 101")]);

    var call = _editor.SingleCall;
    Assert.Equal(("separate_actuals", true), (call.Tool, call.Saves));
    var sent = Assert.Single(ListArgument(call, "actuals"));
    Assert.Equal(new Dictionary<string, object?> { ["item_description"] = "Groceries", ["actual_description"] = "Check 101" }.OrderBy(a => a.Key), sent.OrderBy(a => a.Key));
  }

  [Fact]
  public async Task SeparateActuals_needs_a_transaction()
  {
    var none = await Assert.ThrowsAsync<McpException>(() => Tools.SeparateActuals(2026, 11, []));
    var empty = await Assert.ThrowsAsync<McpException>(() => Tools.SeparateActuals(2026, 11, [null!]));

    Assert.Equal("actuals must list at least 1 transaction.", none.Message);
    Assert.Equal("actuals entry 1 is empty.", empty.Message);
    Assert.Empty(_editor.Calls);
  }

  // ---- Moving, account links, order and cash ----

  [Fact]
  public async Task AddActual_for_a_cash_withdrawal()
  {
    await Tools.AddActual(2026, 11, "Groceries", 60m, "Market", "2026-11-22", isCashWithdrawal: true);

    Assert.Equal(true, _editor.SingleCall.Arguments["is_envelope_deposit"]);
  }

  [Fact]
  public async Task MoveActual()
  {
    await Tools.MoveActual(2026, 11, "Groceries", "Chipotle", " Restaurants ", toCategoryDescription: "Food", actualAmount: 14.50m);

    AssertCall(
      "move_actual",
      new()
      {
        ["item_description"] = "Groceries",
        ["actual_description"] = "Chipotle",
        ["to_item_description"] = "Restaurants",
        ["category_description"] = null,
        ["to_category_description"] = "Food",
        ["actual_date"] = null,
        ["actual_amount"] = 14.50m,
      }
    );
  }

  [Fact]
  public async Task SetActualAccount_with_no_account_asks_for_the_link_to_be_removed()
  {
    await Tools.SetActualAccount(2026, 11, "Groceries", "ATM", actualDate: "2026-11-22");

    AssertCall(
      "set_actual_account",
      new()
      {
        ["item_description"] = "Groceries",
        ["actual_description"] = "ATM",
        ["account_link_id"] = null,
        ["account_category_link_id"] = null,
        ["category_description"] = null,
        ["actual_date"] = "2026-11-22",
        ["actual_amount"] = null,
      }
    );
  }

  [Fact]
  public async Task SetItemAccount()
  {
    const string account = "11111111-1111-1111-1111-111111111111";

    await Tools.SetItemAccount(2026, 11, "Groceries", account, categoryDescription: "Food");

    AssertCall(
      "set_item_account",
      new()
      {
        ["item_description"] = "Groceries",
        ["account_link_id"] = account,
        ["account_category_link_id"] = null,
        ["category_description"] = "Food",
      }
    );
  }

  [Fact]
  public async Task ReorderCategory_and_ReorderItem()
  {
    await Tools.ReorderCategory(2026, 11, "Savings", 1);
    await Tools.ReorderItem(2026, 11, "Medicine", 2, "Food");

    Assert.Equal(["reorder_category", "reorder_item"], _editor.Calls.Select(call => call.Tool));
    Assert.Equal(("Savings", 1), (_editor.Calls[0].Arguments["category_description"], _editor.Calls[0].Arguments["position"]));
    Assert.Equal(("Medicine", 2, "Food"), (_editor.Calls[1].Arguments["item_description"], _editor.Calls[1].Arguments["position"], _editor.Calls[1].Arguments["category_description"]));
  }

  [Fact]
  public async Task A_position_counts_from_one()
  {
    var category = await Assert.ThrowsAsync<McpException>(() => Tools.ReorderCategory(2026, 11, "Savings", 0));
    var item = await Assert.ThrowsAsync<McpException>(() => Tools.ReorderItem(2026, 11, "Medicine", -1));

    Assert.StartsWith("position must be between 1 and 1000", category.Message);
    Assert.StartsWith("position must be between 1 and 1000", item.Message);
    Assert.Empty(_editor.Calls);
  }
}
