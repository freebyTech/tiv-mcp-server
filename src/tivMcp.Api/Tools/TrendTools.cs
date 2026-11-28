using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tools for following budget items across months. Each is a pass-through to one of the
/// trackItsValue API's price trending endpoints and returns the API's JSON unchanged. Price
/// trending unlocks at level 4; the API decides that, and refuses below it.
/// </summary>
[McpServerToolType]
public sealed class TrendTools
{
  /// <summary>The API reads at most this many ids from one trends request.</summary>
  public const int MostItemsAtOnce = 50;

  private const string LevelHint = " Needs level 4 (price trending) in trackItsValue; below that the call is refused. get_user_status shows the user's level.";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public TrendTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET budgeting/lineage/groups
  /// </summary>
  [McpServerTool(Name = "list_trend_items", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List the budget items that can be followed across months, for questions such as how much was spent on groceries over the last three months. "
      + "Each entry is one item as it runs through every month's budget, with the id (continuanceId) to give to get_item_trends, its current name and category, "
      + "and the other names it has had (aliases), so a renamed item is still one entry. lastUsed is the latest month it appears in, written as YYYYMM. "
      + "Call this first to find the ids, then get_item_trends for the amounts."
      + LevelHint
  )]
  public Task<string> ListTrendItems(CancellationToken cancellationToken = default)
  {
    return _tivApi.GetJsonAsync("budgeting/lineage/groups", cancellationToken: cancellationToken);
  }

  /// <summary>
  /// GET budgeting/lineage/trends?continuanceIds=
  /// </summary>
  [McpServerTool(Name = "get_item_trends", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get, for each given budget item, the amount budgeted and the amount actually spent or received in every month on record. "
      + "Use it to total or compare an item across months without reading each month's budget. "
      + "The ids come from list_trend_items. A month marked isClosed is final; an open month can still change. "
      + "It gives monthly totals for items, not single transactions."
      + LevelHint
  )]
  public Task<string> GetItemTrends(
    [Description("The ids (continuanceId) of the items to follow, from list_trend_items. At least 1 and at most 50.")] string[] itemIds,
    CancellationToken cancellationToken = default
  )
  {
    if (itemIds == null || itemIds.Length == 0)
    {
      throw new McpException($"{nameof(itemIds)} must list at least one id from list_trend_items.");
    }

    if (itemIds.Length > MostItemsAtOnce)
    {
      throw new McpException($"{nameof(itemIds)} can list at most {MostItemsAtOnce} ids, but {itemIds.Length} were given.");
    }

    // The API quietly drops anything that is not an id, which would return fewer items than asked for without saying so.
    var ids = new List<string>(itemIds.Length);
    foreach (var itemId in itemIds)
    {
      if (!Guid.TryParse(itemId?.Trim(), out var id) || id == Guid.Empty)
      {
        throw new McpException($"{nameof(itemIds)} contains '{itemId}', which is not an id. Use the continuanceId values from list_trend_items.");
      }

      ids.Add(id.ToString());
    }

    return _tivApi.GetJsonAsync("budgeting/lineage/trends", new Dictionary<string, string> { ["continuanceIds"] = string.Join(',', ids.Distinct()) }, cancellationToken);
  }
}
