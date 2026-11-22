using System.ComponentModel;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tools over the trackItsValue API's account endpoints. Each one checks its arguments,
/// calls one endpoint as the signed-in user and returns the API's JSON as it is.
/// </summary>
[McpServerToolType]
public sealed class AccountTools
{
  private const string AccountsPath = "accounting/accounts";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public AccountTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET accounting/accounts/all
  /// </summary>
  [McpServerTool(Name = "list_accounts", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List the signed-in user's accounts (checking, savings, credit cards, loans, investments and so on) with their names, types and categories. "
      + "This is the plain list, without balances for a particular month. For balances and the month's transactions, use get_accounts_overview."
  )]
  public Task<string> ListAccounts(CancellationToken cancellationToken = default)
  {
    return _tivApi.GetJsonAsync($"{AccountsPath}/all", cancellationToken: cancellationToken);
  }

  /// <summary>
  /// GET accounting/accounts/all/{year}/{month}
  /// </summary>
  [McpServerTool(Name = "get_accounts_overview", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get all of the signed-in user's accounts for one month, grouped by account type, with each account's balances at the start and end of the month and the actuals (deposits and withdrawals) recorded in that month. "
      + "Use this to answer questions about account balances, net worth at a point in time, or what moved through an account in a month. The result can be large."
  )]
  public Task<string> GetAccountsOverview(
    [Description("The four-digit year, for example 2026.")] int year,
    [Description("The month, 1 (January) to 12 (December).")] int month,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);

    return _tivApi.GetJsonAsync($"{AccountsPath}/all/{year}/{month}", cancellationToken: cancellationToken);
  }
}
