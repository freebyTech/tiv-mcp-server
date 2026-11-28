using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Tools that manage the user's accounts the way the website's account page does. Most mirror a
/// tool of the trackItsValue agent API, with the same name and arguments plus the year and month
/// that say which month's accounts to work on; they check the shape of their arguments and hand
/// the rest to <see cref="IAccountEditor"/>. A few call one trackItsValue API endpoint directly.
/// Either way the agent API and the API hold the rules and decide what is allowed.
/// </summary>
[McpServerToolType]
public sealed class AccountManagementTools
{
  private const string AccountsPath = "accounting/accounts";

  private const string SaveHint = " If this fails with 'The save may or may not have gone through', check with get_accounts_summary before retrying, so nothing is recorded twice.";
  private const string NamesHint = " Use the account, category and transaction names shown by get_accounts_summary." + SaveHint;
  private const string YearHint = "The four-digit year of the accounts month to work in, for example 2026.";
  private const string MonthHint = "The month to work in, 1 (January) to 12 (December). A month that has been closed cannot be changed.";
  private const string AccountHint = "The account's name as shown by get_accounts_summary, for example 'Main Checking'.";
  private const string CategoryHint = "The account category (fund) it is in. Only needed to tell apart transactions with the same description.";
  private const string TransactionDateHint = "The transaction's date written as YYYY-MM-DD, to tell apart transactions with the same description.";
  private const string TransactionAmountHint = "The transaction's amount, to tell apart transactions with the same description.";
  private const string BudgetOwnedHint = "A transaction marked [linked] came from the budget: change or delete it there (update_actual, delete_actual), not here. ";

  /// <summary>The most transactions the combine and separate tools take in one call.</summary>
  public const int MostTransactionsAtOnce = 50;

  /// <summary>The most balances set_account_balance takes in one call.</summary>
  public const int MostSnapshotsAtOnce = 24;

  private readonly IAccountEditor _accountEditor;
  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public AccountManagementTools(IAccountEditor accountEditor, ITivApiClient tivApi)
  {
    _accountEditor = accountEditor;
    _tivApi = tivApi;
  }

  /// <summary>
  /// Agent API account tool list_account_options.
  /// </summary>
  [McpServerTool(Name = "list_account_options", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List what an account can be made of: every account type with its sub types, and for each sub type its category types and its deposit and withdrawal types. "
      + "Call it before create_account, add_account_category, or add_account_transaction with a transactionType. "
      + "This is for preparing a change, so the connection needs permission to make changes."
  )]
  public async Task<string> ListAccountOptions([Description(YearHint)] int year, [Description(MonthHint)] int month, CancellationToken cancellationToken = default)
  {
    var data = await _accountEditor.QueryAsync(year, month, "list_account_options", new Dictionary<string, object?>(), cancellationToken);

    return data.ValueKind == JsonValueKind.Undefined ? "{}" : data.GetRawText();
  }

  /// <summary>
  /// Agent API account tool build_new_account, then POST accounting/accounts/createAccount.
  /// </summary>
  [McpServerTool(Name = "create_account", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Create a new account, for example a savings account or a credit card. It starts with one category and no transactions; "
      + "to give it an opening balance use set_account_balance or add_account_transaction afterwards. "
      + "Call list_account_options for the account types and sub types. Two accounts of the same type cannot share a name. "
      + "If this fails after 'Creating the account failed', check with list_accounts before retrying, so the account is not made twice."
  )]
  public async Task<string> CreateAccount(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The account type, for example 'Bank Account', exactly as list_account_options gives it.")] string accountType,
    [Description("The sub type within that account type, for example 'Checking Account', exactly as list_account_options gives it.")] string accountSubType,
    [Description("The account's name, at most 128 characters. Leave empty to use the sub type's name.")] string? description = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["account_type"] = ToolArguments.RequireText(nameof(accountType), accountType),
      ["account_sub_type"] = ToolArguments.RequireText(nameof(accountSubType), accountSubType),
      ["description"] = ToolArguments.OptionalText(nameof(description), description),
    };

    // The agent API works out the account to create; the API creates it, as it does for the website.
    var data = await _accountEditor.QueryAsync(year, month, "build_new_account", arguments, cancellationToken);
    if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("newAccount", out var newAccount) || newAccount.ValueKind != JsonValueKind.Object)
    {
      throw new McpException("trackItsValue returned a new account this server could not read. Nothing was saved.");
    }

    var body = $"{{\"year\":{year},\"month\":{month},\"newAccount\":{newAccount.GetRawText()}}}";
    try
    {
      await _tivApi.PostJsonAsync($"{AccountsPath}/createAccount", body, cancellationToken);
    }
    catch (Services.TivCallFailedException failure)
    {
      throw failure.During("Creating the account failed.", failure.OutcomeUnknown ? "The account may or may not have been created. Check with list_accounts before retrying." : "Nothing was saved.");
    }

    var name = newAccount.TryGetProperty("description", out var given) ? given.GetString() : null;
    return $"Saved. Created account '{name}'.";
  }

  /// <summary>
  /// Agent API account tool rename_account.
  /// </summary>
  [McpServerTool(Name = "rename_account", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Rename an account. Two accounts of the same type cannot share a name." + NamesHint)]
  public Task<string> RenameAccount(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The account's new name, at most 128 characters.")] string newDescription,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["account"] = ToolArguments.RequireText(nameof(account), account),
      ["new_description"] = ToolArguments.RequireText(nameof(newDescription), newDescription),
    };

    return _accountEditor.ApplyAndSaveAsync(year, month, "rename_account", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool remove_account.
  /// </summary>
  [McpServerTool(Name = "remove_account", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Remove an account from view; confirm with the user first. An account that has ever had a transaction is hidden and keeps its history; "
      + "one that never had any is deleted for good. Only an account whose balance is zero at the start and end of the month can be removed. "
      + "A hidden account cannot be brought back with these tools."
      + NamesHint
  )]
  public Task<string> RemoveAccount([Description(YearHint)] int year, [Description(MonthHint)] int month, [Description(AccountHint)] string account, CancellationToken cancellationToken = default)
  {
    var arguments = new Dictionary<string, object?> { ["account"] = ToolArguments.RequireText(nameof(account), account) };

    return _accountEditor.ApplyAndSaveAsync(year, month, "remove_account", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool add_account_category.
  /// </summary>
  [McpServerTool(Name = "add_account_category", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Add a category (a fund) to an account, for example a 'Vacation' goal inside a savings account. A category cannot be deleted afterwards, only renamed. "
      + "When the account's sub type offers more than one category type, name one; list_account_options shows them."
      + NamesHint
  )]
  public Task<string> AddAccountCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The new category's name, at most 128 characters. Leave empty to use the category type's name.")] string? description = null,
    [Description("The category type, exactly as list_account_options gives it. Only needed when the account offers more than one.")] string? categoryType = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["account"] = ToolArguments.RequireText(nameof(account), account),
      ["description"] = ToolArguments.OptionalText(nameof(description), description),
      ["category_type"] = ToolArguments.OptionalText(nameof(categoryType), categoryType),
    };

    return _accountEditor.ApplyAndSaveAsync(year, month, "add_account_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool rename_account_category.
  /// </summary>
  [McpServerTool(Name = "rename_account_category", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Rename a category (a fund) of an account." + NamesHint)]
  public Task<string> RenameAccountCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The category's current name.")] string category,
    [Description("The category's new name, at most 128 characters.")] string newDescription,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["account"] = ToolArguments.RequireText(nameof(account), account),
      ["category"] = ToolArguments.RequireText(nameof(category), category),
      ["new_description"] = ToolArguments.RequireText(nameof(newDescription), newDescription),
    };

    return _accountEditor.ApplyAndSaveAsync(year, month, "rename_account_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool add_account_transaction.
  /// </summary>
  [McpServerTool(Name = "add_account_transaction", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Add a transaction directly to an account: money in (deposit) or money out (withdrawal). "
      + "Use it for something that is not part of the budget, such as interest earned, a fee or a correction. "
      + "Income and spending belong in the budget (add_actual), which posts them to the account by itself; adding them here as well would count them twice. "
      + "To move money between two accounts use transfer_between_accounts. "
      + "The amount is always positive and direction says which way the money goes."
      + NamesHint
  )]
  public Task<string> AddAccountTransaction(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("'deposit' for money in, 'withdrawal' for money out.")] string direction,
    [Description("The amount, as a positive number.")] decimal amount,
    [Description("The transaction date written as YYYY-MM-DD.")] string date,
    [Description("What the transaction was, at most 256 characters. Leave empty to use the transaction type's name.")] string? description = null,
    [Description("The account category (fund) to put it in. Leave empty for the account's default category.")] string? category = null,
    [Description("The transaction type, exactly as list_account_options gives it, for example 'Interest'. Leave empty for the account's standard deposit or withdrawal.")]
      string? transactionType = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["account"] = ToolArguments.RequireText(nameof(account), account),
      ["direction"] = ToolArguments.RequireOneOf(nameof(direction), direction, "deposit", "withdrawal"),
      ["amount"] = amount,
      ["date"] = ToolArguments.RequireDate(nameof(date), date),
      ["description"] = ToolArguments.OptionalText(nameof(description), description),
      ["category"] = ToolArguments.OptionalText(nameof(category), category),
      ["transaction_type"] = ToolArguments.OptionalText(nameof(transactionType), transactionType),
    };

    return _accountEditor.ApplyAndSaveAsync(year, month, "add_account_transaction", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool update_account_transaction.
  /// </summary>
  [McpServerTool(Name = "update_account_transaction", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Change a transaction of an account: its amount, description, date or type, or move it to another category of the same account. "
      + "A deposit stays a deposit and a withdrawal a withdrawal. "
      + BudgetOwnedHint
      + "The transaction is found by its description; when several match, give its date, amount or category."
      + NamesHint
  )]
  public Task<string> UpdateAccountTransaction(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The transaction's current description.")] string transactionDescription,
    [Description("The new amount, as a positive number. Leave empty to keep the current amount.")] decimal? newAmount = null,
    [Description("The new description. Leave empty to keep the current description.")] string? newDescription = null,
    [Description("The new date written as YYYY-MM-DD. Leave empty to keep the current date.")] string? newDate = null,
    [Description("The new transaction type, of the same direction, exactly as list_account_options gives it. Leave empty to keep the current type.")] string? newTransactionType = null,
    [Description("The category of this account to move the transaction to. Leave empty to leave it where it is.")] string? newCategory = null,
    [Description(CategoryHint)] string? category = null,
    [Description(TransactionDateHint)] string? transactionDate = null,
    [Description(TransactionAmountHint)] decimal? transactionAmount = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = Named(account, transactionDescription, category, transactionDate, transactionAmount);
    arguments["new_amount"] = newAmount;
    arguments["new_description"] = ToolArguments.OptionalText(nameof(newDescription), newDescription);
    arguments["new_date"] = ToolArguments.OptionalDate(nameof(newDate), newDate);
    arguments["new_transaction_type"] = ToolArguments.OptionalText(nameof(newTransactionType), newTransactionType);
    arguments["new_category"] = ToolArguments.OptionalText(nameof(newCategory), newCategory);

    return _accountEditor.ApplyAndSaveAsync(year, month, "update_account_transaction", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool delete_account_transaction.
  /// </summary>
  [McpServerTool(Name = "delete_account_transaction", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Delete a transaction that was entered directly in an account. This cannot be undone; confirm with the user first. "
      + BudgetOwnedHint
      + "One side of a transfer between accounts is deleted on its own; delete the other side in the other account too."
      + NamesHint
  )]
  public Task<string> DeleteAccountTransaction(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The description of the transaction to delete.")] string transactionDescription,
    [Description(CategoryHint)] string? category = null,
    [Description(TransactionDateHint)] string? transactionDate = null,
    [Description(TransactionAmountHint)] decimal? transactionAmount = null,
    CancellationToken cancellationToken = default
  )
  {
    return _accountEditor.ApplyAndSaveAsync(year, month, "delete_account_transaction", Named(account, transactionDescription, category, transactionDate, transactionAmount), cancellationToken);
  }

  /// <summary>
  /// Agent API account tool set_account_transaction_verified.
  /// </summary>
  [McpServerTool(Name = "set_account_transaction_verified", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Mark ONE transaction of an account as verified, meaning checked against the bank or card statement, or clear that mark. "
      + "To mark every transaction of an account in a month use verify_account_transactions instead."
      + NamesHint
  )]
  public Task<string> SetAccountTransactionVerified(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The transaction's description.")] string transactionDescription,
    [Description("True (the default) marks it verified; false clears the mark.")] bool verified = true,
    [Description(CategoryHint)] string? category = null,
    [Description(TransactionDateHint)] string? transactionDate = null,
    [Description(TransactionAmountHint)] decimal? transactionAmount = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = Named(account, transactionDescription, category, transactionDate, transactionAmount);
    arguments["verified"] = verified;

    return _accountEditor.ApplyAndSaveAsync(year, month, "set_account_transaction_verified", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool combine_account_transactions.
  /// </summary>
  [McpServerTool(Name = "combine_account_transactions", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Mark transactions of ONE account as one combined transaction, so they match a single line on the bank or card statement; "
      + "get_accounts_summary then shows them with the same [combined:...] tag. "
      + "If one of them is already combined, the others are added to that combination. They must all have the same date. "
      + "This works on any transaction of the account, also ones that came from the budget; no amount or date changes."
      + NamesHint
  )]
  public Task<string> CombineAccountTransactions(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The transactions to combine, at least 2 and at most 50.")] AccountTransactionReference[] transactions,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["account"] = ToolArguments.RequireText(nameof(account), account), ["transactions"] = References(transactions, 2) };

    return _accountEditor.ApplyAndSaveAsync(year, month, "combine_account_transactions", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool separate_account_transactions.
  /// </summary>
  [McpServerTool(Name = "separate_account_transactions", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Take transactions of an account out of the combined transaction they belong to. The transactions themselves stay, with their amounts. "
      + "Other members stay combined with each other."
      + NamesHint
  )]
  public Task<string> SeparateAccountTransactions(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(AccountHint)] string account,
    [Description("The transactions to take out of their combined transaction, at least 1 and at most 50.")] AccountTransactionReference[] transactions,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["account"] = ToolArguments.RequireText(nameof(account), account), ["transactions"] = References(transactions, 1) };

    return _accountEditor.ApplyAndSaveAsync(year, month, "separate_account_transactions", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool transfer_between_accounts.
  /// </summary>
  [McpServerTool(Name = "transfer_between_accounts", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Move money directly from one account to another, outside the budget: a withdrawal from one and a matching deposit in the other, both recorded as already verified. "
      + "Use it for a move that is neither income nor spending, such as shifting money between two savings accounts. "
      + "A transfer the budget plans for (a savings or loan payment item) belongs in the budget with add_actual. "
      + "The user needs level 2 for this; the API says so when they do not have it."
      + NamesHint
  )]
  public Task<string> TransferBetweenAccounts(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the account the money leaves.")] string fromAccount,
    [Description("The name of the account the money goes to.")] string toAccount,
    [Description("The amount, as a positive number.")] decimal amount,
    [Description("The date of the transfer written as YYYY-MM-DD.")] string date,
    [Description("The category of fromAccount the money leaves. Leave empty for its default category.")] string? fromCategory = null,
    [Description("The category of toAccount the money goes into. Leave empty for its default category.")] string? toCategory = null,
    [Description("A description for both sides, at most 256 characters. Leave empty for 'Transfer to ...' and 'Transfer from ...'.")] string? description = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["from_account"] = ToolArguments.RequireText(nameof(fromAccount), fromAccount),
      ["to_account"] = ToolArguments.RequireText(nameof(toAccount), toAccount),
      ["amount"] = amount,
      ["date"] = ToolArguments.RequireDate(nameof(date), date),
      ["from_category"] = ToolArguments.OptionalText(nameof(fromCategory), fromCategory),
      ["to_category"] = ToolArguments.OptionalText(nameof(toCategory), toCategory),
      ["description"] = ToolArguments.OptionalText(nameof(description), description),
    };

    return _accountEditor.ApplyAndSaveAsync(year, month, "transfer_between_accounts", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API account tool find_account, then POST accounting/accounts/{accountId}/set-balance-as-of.
  /// </summary>
  [McpServerTool(Name = "set_account_balance", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Set what an account's balance was on one or more dates, the website's 'Set Value'. trackItsValue adds a growth or loss entry on each date to reach that balance, "
      + "and adjusts the next entry after it so later balances stay as they were. Use it for accounts whose value moves by itself, such as investments or a house, and to give a new account its opening balance. "
      + "With apply left false nothing changes and the answer is a preview of the entries it would make: always preview first and show the user. "
      + "Set apply to true to make the change; it is refused when it would flip a later entry's direction or touch a closed month. "
      + "The user needs level 2 for this; the API says so when they do not have it."
  )]
  public async Task<string> SetAccountBalance(
    [Description(AccountHint)] string account,
    [Description("The balances to set, at least 1 and at most 24, each with its date.")] BalanceSnapshot[] balances,
    [Description("False (the default) only previews; true makes the change.")] bool apply = false,
    [Description("The account category (fund) the balances are for. Leave empty for the account's default category.")] string? category = null,
    CancellationToken cancellationToken = default
  )
  {
    var named = ToolArguments.RequireText(nameof(account), account);
    var inCategory = ToolArguments.OptionalText(nameof(category), category);
    if (balances == null || balances.Length == 0)
    {
      throw new McpException($"{nameof(balances)} must list at least one balance with its date.");
    }
    if (balances.Length > MostSnapshotsAtOnce)
    {
      throw new McpException($"{nameof(balances)} can list at most {MostSnapshotsAtOnce} balances, but {balances.Length} were given.");
    }

    var dates = new List<string>(balances.Length);
    for (var index = 0; index < balances.Length; index++)
    {
      var entry = $"{nameof(balances)} entry {index + 1}";
      var balance = balances[index] ?? throw new McpException($"{entry} is empty.");
      dates.Add(ToolArguments.RequireDate($"{entry}: date", balance.Date));
    }

    // The API takes the account by id, so the agent API finds it by name first, in the month of
    // the first balance.
    var first = DateOnly.ParseExact(dates[0], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    var lookup = new Dictionary<string, object?> { ["account"] = named, ["category"] = inCategory };
    var data = await _accountEditor.QueryAsync(first.Year, first.Month, "find_account", lookup, cancellationToken);
    var found = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("account", out var element) ? element.Deserialize<FoundAccount>() : null;
    if (string.IsNullOrWhiteSpace(found?.Id))
    {
      throw new McpException("trackItsValue returned an account this server could not read. Nothing was saved.");
    }

    var request = new SetBalanceAsOfRequest
    {
      Snapshots = balances
        .Select(
          (balance, index) =>
            new SetBalanceAsOfSnapshot
            {
              CategoryId = inCategory == null ? null : found.CategoryId,
              Date = dates[index],
              TargetBalance = balance.TargetBalance,
            }
        )
        .ToList(),
      ApplyConfirmed = apply,
      ReopenClosedMonths = false,
    };

    return await _tivApi.PostJsonAsync($"{AccountsPath}/{Uri.EscapeDataString(found.Id)}/set-balance-as-of", JsonSerializer.Serialize(request), cancellationToken);
  }

  /// <summary>
  /// GET accounting/accounts/months-status
  /// </summary>
  [McpServerTool(Name = "get_months_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List recent months with whether each month's budget and accounts are closed, can be closed, or are blocked and why (for example transactions not yet verified). "
      + "Use it before close_budget or close_account_month. The user needs level 3 for this; the API says so when they do not have it."
  )]
  public Task<string> GetMonthsStatus(
    [Description("False (the default) covers the last two years; true goes back to the user's first month, which needs a paid plan.")] bool allMonths = false,
    CancellationToken cancellationToken = default
  )
  {
    var query = new Dictionary<string, string> { ["range"] = allMonths ? "all" : "2y" };

    return _tivApi.GetJsonAsync($"{AccountsPath}/months-status", query, cancellationToken);
  }

  /// <summary>
  /// POST accounting/accounts/close-month/{year}/{month}
  /// </summary>
  [McpServerTool(Name = "close_account_month", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Close a month for all of the user's accounts: it becomes read-only and its balances are fixed for the dashboards. Confirm with the user first. "
      + "The month's budget must be closed first (close_budget), and every account transaction in the month must be verified (verify_account_transactions). "
      + "The API refuses with the reason when either is not so. reopen_account_month undoes it."
  )]
  public Task<string> CloseAccountMonth(
    [Description("The four-digit year of the month to close.")] int year,
    [Description("The month to close, 1 (January) to 12 (December).")] int month,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);

    return _tivApi.PostJsonAsync($"{AccountsPath}/close-month/{year}/{month}", "{}", cancellationToken);
  }

  /// <summary>
  /// POST accounting/accounts/reopen-month/{year}/{month}
  /// </summary>
  [McpServerTool(Name = "reopen_account_month", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Reopen a closed month for all of the user's accounts, so its transactions can be changed again. Confirm with the user first: "
      + "the experience points earned by closing it are taken back. The month's budget stays closed until reopen_budget."
  )]
  public Task<string> ReopenAccountMonth(
    [Description("The four-digit year of the month to reopen.")] int year,
    [Description("The month to reopen, 1 (January) to 12 (December).")] int month,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);

    return _tivApi.PostJsonAsync($"{AccountsPath}/reopen-month/{year}/{month}", "{}", cancellationToken);
  }

  /// <summary>
  /// The arguments that name one transaction of an account, under the agent API's names.
  /// </summary>
  private static Dictionary<string, object?> Named(string account, string transactionDescription, string? category, string? transactionDate, decimal? transactionAmount)
  {
    return new Dictionary<string, object?>
    {
      ["account"] = ToolArguments.RequireText(nameof(account), account),
      ["transaction_description"] = ToolArguments.RequireText(nameof(transactionDescription), transactionDescription),
      ["category"] = ToolArguments.OptionalText(nameof(category), category),
      ["transaction_date"] = ToolArguments.OptionalDate(nameof(transactionDate), transactionDate),
      ["transaction_amount"] = transactionAmount,
    };
  }

  /// <summary>
  /// Checks a list of named transactions and returns it as the agent API's arguments.
  /// </summary>
  private static List<Dictionary<string, object?>> References(AccountTransactionReference[] transactions, int fewest)
  {
    if (transactions == null || transactions.Length < fewest)
    {
      throw new McpException($"{nameof(transactions)} must list at least {fewest} transaction{(fewest == 1 ? string.Empty : "s")}.");
    }
    if (transactions.Length > MostTransactionsAtOnce)
    {
      throw new McpException($"{nameof(transactions)} can list at most {MostTransactionsAtOnce} transactions, but {transactions.Length} were given.");
    }

    var references = new List<Dictionary<string, object?>>(transactions.Length);
    for (var index = 0; index < transactions.Length; index++)
    {
      var entry = $"{nameof(transactions)} entry {index + 1}";
      var transaction = transactions[index] ?? throw new McpException($"{entry} is empty.");
      var reference = new Dictionary<string, object?>
      {
        ["transaction_description"] = ToolArguments.RequireText($"{entry}: transactionDescription", transaction.TransactionDescription),
        ["category"] = ToolArguments.OptionalText($"{entry}: category", transaction.Category),
        ["transaction_date"] = ToolArguments.OptionalDate($"{entry}: transactionDate", transaction.TransactionDate),
        ["transaction_amount"] = transaction.TransactionAmount,
      };
      references.Add(reference.Where(argument => argument.Value != null).ToDictionary(argument => argument.Key, argument => argument.Value));
    }

    return references;
  }
}
