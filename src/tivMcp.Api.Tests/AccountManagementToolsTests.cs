using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Models;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

/// <summary>
/// Each account tool must name the agent API tool it mirrors and pass that tool's arguments under
/// the agent API's own names, or make the one API call it stands for. Finding accounts and
/// deciding what is allowed is never done here.
/// </summary>
public class AccountManagementToolsTests
{
  private readonly FakeAccountEditor _editor = new();
  private readonly FakeTivApiClient _api = new();

  private AccountManagementTools Tools => new(_editor, _api);

  private void AssertSaved(string tool, Dictionary<string, object?> arguments)
  {
    var call = _editor.SingleCall;
    Assert.Equal((2026, 11, tool, true), (call.Year, call.Month, call.Tool, call.Saves));
    Assert.Equal(arguments.OrderBy(a => a.Key), call.Arguments.OrderBy(a => a.Key));
    Assert.Empty(_api.Posts);
    Assert.Empty(_api.Puts);
  }

  private static AccountTransactionReference Named(string description, decimal? amount = null, string? date = null) =>
    new()
    {
      TransactionDescription = description,
      TransactionAmount = amount,
      TransactionDate = date,
    };

  [Fact]
  public async Task AddAccountTransaction()
  {
    await Tools.AddAccountTransaction(2026, 11, " Main Checking ", "Deposit", 4.12m, "2026-11-30", "Interest earned", transactionType: "Interest");

    AssertSaved(
      "add_account_transaction",
      new()
      {
        ["account"] = "Main Checking",
        ["direction"] = "deposit",
        ["amount"] = 4.12m,
        ["date"] = "2026-11-30",
        ["description"] = "Interest earned",
        ["category"] = null,
        ["transaction_type"] = "Interest",
      }
    );
  }

  [Fact]
  public async Task AddAccountTransaction_checks_the_direction_and_the_date_before_anything_is_sent()
  {
    var direction = await Assert.ThrowsAsync<McpException>(() => Tools.AddAccountTransaction(2026, 11, "Main Checking", "in", 5m, "2026-11-30"));
    var date = await Assert.ThrowsAsync<McpException>(() => Tools.AddAccountTransaction(2026, 11, "Main Checking", "deposit", 5m, "30/11/2026"));
    var account = await Assert.ThrowsAsync<McpException>(() => Tools.AddAccountTransaction(2026, 11, " ", "deposit", 5m, "2026-11-30"));

    Assert.Equal("direction must be one of: deposit, withdrawal.", direction.Message);
    Assert.StartsWith("date must be a date written as YYYY-MM-DD", date.Message);
    Assert.Equal("account is required.", account.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task UpdateAccountTransaction_passes_only_what_was_given()
  {
    await Tools.UpdateAccountTransaction(2026, 11, "Main Checking", "Check 101", newAmount: 30m, newCategory: "Vacation", transactionAmount: 25m);

    AssertSaved(
      "update_account_transaction",
      new()
      {
        ["account"] = "Main Checking",
        ["transaction_description"] = "Check 101",
        ["category"] = null,
        ["transaction_date"] = null,
        ["transaction_amount"] = 25m,
        ["new_amount"] = 30m,
        ["new_description"] = null,
        ["new_date"] = null,
        ["new_transaction_type"] = null,
        ["new_category"] = "Vacation",
      }
    );
  }

  [Fact]
  public async Task DeleteAccountTransaction()
  {
    await Tools.DeleteAccountTransaction(2026, 11, "Main Checking", "Check 101", "General Fund", "2026-11-23");

    AssertSaved(
      "delete_account_transaction",
      new()
      {
        ["account"] = "Main Checking",
        ["transaction_description"] = "Check 101",
        ["category"] = "General Fund",
        ["transaction_date"] = "2026-11-23",
        ["transaction_amount"] = null,
      }
    );
  }

  [Fact]
  public async Task SetAccountTransactionVerified()
  {
    await Tools.SetAccountTransactionVerified(2026, 11, "Main Checking", "Check 101", verified: false);

    AssertSaved(
      "set_account_transaction_verified",
      new()
      {
        ["account"] = "Main Checking",
        ["transaction_description"] = "Check 101",
        ["category"] = null,
        ["transaction_date"] = null,
        ["transaction_amount"] = null,
        ["verified"] = false,
      }
    );
  }

  [Theory]
  [InlineData("combine_account_transactions")]
  [InlineData("separate_account_transactions")]
  public async Task Combining_and_separating_name_each_transaction(string tool)
  {
    AccountTransactionReference[] named = [Named(" ATM ", 60m, "2026-11-22"), Named("ATM", 40m)];

    await (tool == "combine_account_transactions" ? Tools.CombineAccountTransactions(2026, 11, "Main Checking", named) : Tools.SeparateAccountTransactions(2026, 11, "Main Checking", named));

    var call = _editor.SingleCall;
    Assert.Equal((tool, true, "Main Checking"), (call.Tool, call.Saves, call.Arguments["account"]));
    var sent = Assert.IsType<List<Dictionary<string, object?>>>(call.Arguments["transactions"]);
    Assert.Equal(
      new Dictionary<string, object?>
      {
        ["transaction_description"] = "ATM",
        ["transaction_date"] = "2026-11-22",
        ["transaction_amount"] = 60m,
      }.OrderBy(a => a.Key),
      sent[0].OrderBy(a => a.Key)
    );
    Assert.Equal(new Dictionary<string, object?> { ["transaction_description"] = "ATM", ["transaction_amount"] = 40m }.OrderBy(a => a.Key), sent[1].OrderBy(a => a.Key));
  }

  [Fact]
  public async Task Combining_needs_two_named_transactions_and_separating_one()
  {
    var one = await Assert.ThrowsAsync<McpException>(() => Tools.CombineAccountTransactions(2026, 11, "Main Checking", [Named("ATM")]));
    var none = await Assert.ThrowsAsync<McpException>(() => Tools.SeparateAccountTransactions(2026, 11, "Main Checking", []));
    var unnamed = await Assert.ThrowsAsync<McpException>(() => Tools.CombineAccountTransactions(2026, 11, "Main Checking", [Named("ATM"), Named(" ")]));
    var empty = await Assert.ThrowsAsync<McpException>(() => Tools.SeparateAccountTransactions(2026, 11, "Main Checking", [null!]));
    var tooMany = await Assert.ThrowsAsync<McpException>(() => Tools.CombineAccountTransactions(2026, 11, "Main Checking", Enumerable.Repeat(Named("ATM"), 51).ToArray()));

    Assert.Equal("transactions must list at least 2 transactions.", one.Message);
    Assert.Equal("transactions must list at least 1 transaction.", none.Message);
    Assert.Equal("transactions entry 2: transactionDescription is required.", unnamed.Message);
    Assert.Equal("transactions entry 1 is empty.", empty.Message);
    Assert.StartsWith("transactions can list at most 50 transactions, but 51 were given.", tooMany.Message);
    Assert.Empty(_editor.Calls);
  }

  [Fact]
  public async Task TransferBetweenAccounts()
  {
    await Tools.TransferBetweenAccounts(2026, 11, "Main Checking", "Rainy Day Savings", 250m, "2026-11-15", fromCategory: "Vacation");

    AssertSaved(
      "transfer_between_accounts",
      new()
      {
        ["from_account"] = "Main Checking",
        ["to_account"] = "Rainy Day Savings",
        ["amount"] = 250m,
        ["date"] = "2026-11-15",
        ["from_category"] = "Vacation",
        ["to_category"] = null,
        ["description"] = null,
      }
    );
  }

  [Fact]
  public async Task Account_and_category_edits_name_their_tools()
  {
    await Tools.RenameAccount(2026, 11, "Main Checking", " Joint Checking ");
    await Tools.RemoveAccount(2026, 11, "Unused Savings");
    await Tools.AddAccountCategory(2026, 11, "Main Checking", "Car Repairs");
    await Tools.RenameAccountCategory(2026, 11, "Main Checking", "Vacation", "Travel");

    Assert.Equal(["rename_account", "remove_account", "add_account_category", "rename_account_category"], _editor.Calls.Select(call => call.Tool));
    Assert.All(_editor.Calls, call => Assert.True(call.Saves));
    Assert.Equal("Joint Checking", _editor.Calls[0].Arguments["new_description"]);
    Assert.Equal("Unused Savings", _editor.Calls[1].Arguments["account"]);
    Assert.Equal(("Car Repairs", null), (_editor.Calls[2].Arguments["description"], _editor.Calls[2].Arguments["category_type"]));
    Assert.Equal(("Vacation", "Travel"), (_editor.Calls[3].Arguments["category"], _editor.Calls[3].Arguments["new_description"]));
  }

  [Fact]
  public async Task ListAccountOptions_asks_the_agent_api_and_saves_nothing()
  {
    _editor.DataToReturn = """{"accountTypes":[{"accountType":"Bank Account"}]}""";

    var answer = await Tools.ListAccountOptions(2026, 11);

    Assert.Equal(("list_account_options", false), (_editor.SingleCall.Tool, _editor.SingleCall.Saves));
    Assert.Equal(_editor.DataToReturn, answer);
  }

  [Fact]
  public async Task CreateAccount_has_the_agent_api_prepare_the_account_and_the_api_create_it()
  {
    _editor.DataToReturn = """{"newAccount":{"id":"a-9", "description":"House Fund","accountTypeId":1,"isNew":true}}""";

    var answer = await Tools.CreateAccount(2026, 11, "Bank Account", "Savings Account", " House Fund ");

    var call = _editor.SingleCall;
    Assert.Equal(("build_new_account", false), (call.Tool, call.Saves));
    Assert.Equal(("Bank Account", "Savings Account", "House Fund"), (call.Arguments["account_type"], call.Arguments["account_sub_type"], call.Arguments["description"]));
    var post = Assert.Single(_api.Posts);
    Assert.Equal("accounting/accounts/createAccount", post.Path);
    // The prepared account is passed on exactly as the agent API wrote it.
    Assert.Equal("""{"year":2026,"month":11,"newAccount":{"id":"a-9", "description":"House Fund","accountTypeId":1,"isNew":true}}""", post.Body);
    Assert.Equal("Saved. Created account 'House Fund'.", answer);
  }

  [Fact]
  public async Task CreateAccount_says_whether_the_account_may_exist_when_the_api_fails()
  {
    _editor.DataToReturn = """{"newAccount":{"description":"House Fund"}}""";

    _api.PostFailure = new TivCallFailedException("The request was refused.", 403, "trackItsValue", "ref12345");
    var refused = await Assert.ThrowsAsync<TivCallFailedException>(() => Tools.CreateAccount(2026, 11, "Bank Account", "Savings Account"));
    _api.PostFailure = new TivCallFailedException("No answer.", 0, "trackItsValue", "ref12345");
    var unknown = await Assert.ThrowsAsync<TivCallFailedException>(() => Tools.CreateAccount(2026, 11, "Bank Account", "Savings Account"));

    Assert.Equal("Creating the account failed. The request was refused. Nothing was saved.", refused.Message);
    Assert.EndsWith("The account may or may not have been created. Check with list_accounts before retrying.", unknown.Message);
  }

  [Fact]
  public async Task CreateAccount_stops_when_the_agent_api_prepared_nothing()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.CreateAccount(2026, 11, "Bank Account", "Savings Account"));

    Assert.EndsWith("Nothing was saved.", error.Message);
    Assert.Empty(_api.Posts);
  }

  [Fact]
  public async Task SetAccountBalance_finds_the_account_by_name_then_asks_the_api_for_a_preview()
  {
    _editor.DataToReturn = """{"account":{"id":"7f0c2f6e-1111-2222-3333-444455556666","description":"Brokerage","categoryId":null}}""";
    _api.JsonToReturn = """{"lines":[]}""";
    BalanceSnapshot[] balances = [new() { Date = "2026-10-31", TargetBalance = 15250.75m }, new() { Date = "2026-11-30", TargetBalance = 15900m }];

    var answer = await Tools.SetAccountBalance(" Brokerage ", balances);

    // Looked up in the month of the first balance.
    var call = _editor.SingleCall;
    Assert.Equal((2026, 10, "find_account", false), (call.Year, call.Month, call.Tool, call.Saves));
    Assert.Equal(("Brokerage", null), (call.Arguments["account"], call.Arguments["category"]));
    var post = Assert.Single(_api.Posts);
    Assert.Equal("accounting/accounts/7f0c2f6e-1111-2222-3333-444455556666/set-balance-as-of", post.Path);
    Assert.Equal("""{"snapshots":[{"date":"2026-10-31","targetBalance":15250.75},{"date":"2026-11-30","targetBalance":15900}],"applyConfirmed":false,"reopenClosedMonths":false}""", post.Body);
    Assert.Equal("""{"lines":[]}""", answer);
  }

  [Fact]
  public async Task SetAccountBalance_applies_to_a_named_category_when_asked()
  {
    _editor.DataToReturn = """{"account":{"id":"acc-1","description":"House","categoryId":"cat-7"}}""";

    await Tools.SetAccountBalance("House", [new() { Date = "2026-11-30", TargetBalance = 410000m }], apply: true, category: "Equity");

    Assert.Equal("Equity", _editor.SingleCall.Arguments["category"]);
    var sent = JsonDocument.Parse(Assert.Single(_api.Posts).Body).RootElement;
    Assert.True(sent.GetProperty("applyConfirmed").GetBoolean());
    Assert.False(sent.GetProperty("reopenClosedMonths").GetBoolean());
    Assert.Equal("cat-7", sent.GetProperty("snapshots")[0].GetProperty("categoryId").GetString());
  }

  [Fact]
  public async Task SetAccountBalance_checks_its_balances_before_any_call()
  {
    var none = await Assert.ThrowsAsync<McpException>(() => Tools.SetAccountBalance("House", []));
    var badDate = await Assert.ThrowsAsync<McpException>(() => Tools.SetAccountBalance("House", [new() { Date = "Nov 30", TargetBalance = 1m }]));
    var empty = await Assert.ThrowsAsync<McpException>(() => Tools.SetAccountBalance("House", [null!]));
    var tooMany = await Assert.ThrowsAsync<McpException>(() => Tools.SetAccountBalance("House", Enumerable.Repeat(new BalanceSnapshot { Date = "2026-11-30" }, 25).ToArray()));

    Assert.StartsWith("balances must list at least one balance", none.Message);
    Assert.StartsWith("balances entry 1: date must be a date written as YYYY-MM-DD", badDate.Message);
    Assert.Equal("balances entry 1 is empty.", empty.Message);
    Assert.StartsWith("balances can list at most 24 balances, but 25 were given.", tooMany.Message);
    Assert.Empty(_editor.Calls);
    Assert.Empty(_api.Posts);
  }

  [Fact]
  public async Task SetAccountBalance_stops_when_no_account_came_back()
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.SetAccountBalance("House", [new() { Date = "2026-11-30", TargetBalance = 1m }]));

    Assert.EndsWith("Nothing was saved.", error.Message);
    Assert.Empty(_api.Posts);
  }

  [Fact]
  public async Task Closing_and_reopening_a_month_are_one_call_each_to_the_api()
  {
    _api.JsonToReturn = """{"xpAwarded":50}""";

    var closed = await Tools.CloseAccountMonth(2026, 10);
    var reopened = await Tools.ReopenAccountMonth(2026, 10);

    Assert.Equal(["accounting/accounts/close-month/2026/10", "accounting/accounts/reopen-month/2026/10"], _api.Posts.Select(post => post.Path));
    Assert.Equal("""{"xpAwarded":50}""", closed);
    Assert.Equal("""{"xpAwarded":50}""", reopened);
    Assert.Empty(_editor.Calls);
    await Assert.ThrowsAsync<McpException>(() => Tools.CloseAccountMonth(2026, 13));
    await Assert.ThrowsAsync<McpException>(() => Tools.ReopenAccountMonth(1999, 10));
  }

  [Theory]
  [InlineData(false, "2y")]
  [InlineData(true, "all")]
  public async Task GetMonthsStatus_asks_for_two_years_unless_all_months_are_wanted(bool allMonths, string range)
  {
    await Tools.GetMonthsStatus(allMonths);

    var call = Assert.Single(_api.Calls);
    Assert.Equal("accounting/accounts/months-status", call.Path);
    Assert.Equal(range, call.Query!["range"]);
  }
}
