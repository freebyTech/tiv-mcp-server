using System.ComponentModel;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tools about the signed-in user and their progress.
/// </summary>
[McpServerToolType]
public sealed class UserTools
{
  private static readonly string[] WealthRanges = ["all", "6mo", "1yr", "2yr", "3yr", "4yr", "5yr", "10yr"];

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public UserTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET user/status
  /// </summary>
  [McpServerTool(Name = "get_user_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get the signed-in user's trackItsValue status: their level and experience points (XP), whether they have new accomplishments, and how many budgets and accounts they have. "
      + "Some features unlock at higher levels, so this also explains why another tool may have been refused."
  )]
  public Task<string> GetUserStatus(CancellationToken cancellationToken = default)
  {
    return _tivApi.GetJsonAsync("user/status", cancellationToken: cancellationToken);
  }

  /// <summary>
  /// GET dashboards/total-wealth?range=
  /// </summary>
  [McpServerTool(Name = "get_total_wealth_outlook", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get the signed-in user's total wealth over time: one point per closed month with the total held in each type of account, the value of holdings, and net wealth. Months that are still open are not included, because their balances are still changing. "
      + "This dashboard unlocks at a higher user level; if the user has not reached it, the call is refused with the reason."
  )]
  public Task<string> GetTotalWealthOutlook(
    [Description("How far back to look: 'all' (default), '6mo', '1yr', '2yr', '3yr', '4yr', '5yr' or '10yr'.")] string range = "all",
    CancellationToken cancellationToken = default
  )
  {
    var checkedRange = ToolArguments.RequireOneOf(nameof(range), range, WealthRanges);

    return _tivApi.GetJsonAsync("dashboards/total-wealth", new Dictionary<string, string> { ["range"] = checkedRange }, cancellationToken);
  }
}
