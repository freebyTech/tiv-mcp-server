using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Tools that change the user's accounts. The trackItsValue API does the work and decides whether
/// it is allowed; a tool here only checks the shape of its arguments, makes one call as the
/// signed-in user and returns the API's JSON as it is.
/// </summary>
[McpServerToolType]
public sealed class AccountEditTools
{
  private const string AccountsPath = "accounting/accounts";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public AccountEditTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// POST accounting/accounts/verify-transactions
  /// </summary>
  [McpServerTool(Name = "verify_account_transactions", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Mark the transactions of one account in one month as verified, meaning checked against the bank or card statement. "
      + "It covers the whole month, or with throughDay the 1st through that day. To cover several months, call it once per month. "
      + "Only that account's transactions change: the other side of a transfer and the budget's transactions are left alone. "
      + "Set dryRun to true to see the counts without changing anything. "
      + "The answer gives markedCount (changed), alreadyInRequestedStateCount (left as they were), remainingInMonthCount (later in the month, not covered), "
      + "remainingInEarlierMonthsCount (earlier months still to do; 0 means nothing further back) and endingBalance (the account's balance at the end of the last day covered, to compare with the statement). "
      + "Set verified to false to undo; that is refused in a month that has been closed. "
      + "Repeating a call is safe: transactions already in the requested state are not touched."
  )]
  public Task<string> VerifyAccountTransactions(
    [Description("The account's exact name as shown by get_accounts_summary or list_accounts, for example 'James Discover Card', or its id. Part of a name is not accepted.")] string account,
    [Description("The four-digit year, for example 2026.")] int year,
    [Description("The month, 1 (January) to 12 (December).")] int month,
    [Description("The last day of the month to cover, 1 to 31. Leave empty for the whole month; a day past the end of the month also means the whole month.")] int? throughDay = null,
    [Description("True (the default) marks the transactions verified; false marks them not verified.")] bool verified = true,
    [Description("True changes nothing and only reports what would change.")] bool dryRun = false,
    CancellationToken cancellationToken = default
  )
  {
    var named = ToolArguments.RequireText(nameof(account), account);
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    if (throughDay.HasValue)
    {
      ToolArguments.RequireRange(nameof(throughDay), throughDay.Value, 1, 31);
    }

    // An id is sent as an id and anything else as a name; finding the account is the API's job.
    var isId = Guid.TryParse(named, out var accountId);
    var request = new VerifyAccountTransactionsRequest
    {
      AccountId = isId ? accountId.ToString() : null,
      AccountName = isId ? null : named,
      Year = year,
      Month = month,
      ThroughDay = throughDay,
      Verified = verified,
      DryRun = dryRun,
    };

    return _tivApi.PostJsonAsync($"{AccountsPath}/verify-transactions", JsonSerializer.Serialize(request), cancellationToken);
  }
}
