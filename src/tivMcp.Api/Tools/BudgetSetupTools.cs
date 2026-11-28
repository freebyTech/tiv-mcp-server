using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Tools that bring a budget into being. The trackItsValue API does the work and decides whether
/// it is allowed; a tool here only checks its arguments and makes the API's calls in order.
/// </summary>
[McpServerToolType]
public sealed class BudgetSetupTools
{
  private const string BudgetsPath = "budgeting/budgets";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public BudgetSetupTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET budgeting/budgets/{description}/{year}/{month} for the source, then POST budgeting/budgets/copy
  /// </summary>
  [McpServerTool(Name = "create_budget", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Create the budget for a month that does not have one yet, as a copy of an earlier month: the same categories, items and budgeted amounts, with no transactions. "
      + "By default the month before is copied; give copyFromYear and copyFromMonth to copy a different month. "
      + "Use this when another tool says there is no budget for the month yet. "
      + "It is refused when the month already has a budget, and it cannot make a user's very first budget: that is done in trackItsValue itself."
  )]
  public async Task<string> CreateBudget(
    [Description("The four-digit year of the budget to create, for example 2026.")] int year,
    [Description("The month of the budget to create, 1 (January) to 12 (December).")] int month,
    [Description("The budget's name. Leave empty for the user's main budget, which is named 'default'. The same name is used for the month copied from.")] string? budgetName = null,
    [Description("The year of the month to copy from. Leave empty, together with copyFromMonth, to copy the month before.")] int? copyFromYear = null,
    [Description("The month to copy from, 1 to 12. Leave empty, together with copyFromYear, to copy the month before.")] int? copyFromMonth = null,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);

    if (copyFromYear.HasValue != copyFromMonth.HasValue)
    {
      throw new McpException("copyFromYear and copyFromMonth must be given together, or both left empty to copy the month before.");
    }

    var (sourceYear, sourceMonth) = copyFromYear.HasValue ? (copyFromYear.Value, copyFromMonth!.Value) : MonthBefore(year, month);
    ToolArguments.RequireRange(nameof(copyFromYear), sourceYear, 2000, 2100);
    ToolArguments.RequireRange(nameof(copyFromMonth), sourceMonth, 1, 12);

    if (sourceYear == year && sourceMonth == month)
    {
      throw new McpException("A budget cannot be created as a copy of its own month. Give a different copyFromYear and copyFromMonth.");
    }

    var sourceJson = await _tivApi.GetJsonAsync($"{BudgetsPath}/{Uri.EscapeDataString(description)}/{sourceYear}/{sourceMonth}", cancellationToken: cancellationToken);
    var source = Read(sourceJson);
    if (source is not { IsNew: false } || string.IsNullOrWhiteSpace(source.Id))
    {
      throw new McpException(
        $"There is no budget for {sourceYear}-{sourceMonth:00} to copy from. Give copyFromYear and copyFromMonth for a month that has one; list_budgets shows the user's budgets."
      );
    }

    var request = new BudgetCopyRequest
    {
      SourceId = source.Id,
      DestinationDescription = description,
      DestinationYear = year,
      DestinationMonth = month,
      // A new month starts with the plan and no spending.
      CopyActuals = false,
    };

    // The API answers with the whole new budget, which is not passed on: get_budget_summary shows it far more cheaply.
    await _tivApi.PostJsonAsync($"{BudgetsPath}/copy", JsonSerializer.Serialize(request), cancellationToken);

    return $"Created the budget for {year}-{month:00} as a copy of {sourceYear}-{sourceMonth:00}, with its categories, items and budgeted amounts and no transactions. "
      + "Use get_budget_summary to see it.";
  }

  private static (int Year, int Month) MonthBefore(int year, int month) => month == 1 ? (year - 1, 12) : (year, month - 1);

  private static BudgetState? Read(string json)
  {
    if (string.IsNullOrWhiteSpace(json))
    {
      return null;
    }

    try
    {
      return JsonSerializer.Deserialize<BudgetState>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
