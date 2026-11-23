using System.ComponentModel;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tools over the trackItsValue API's budget endpoints. Each one checks its arguments,
/// calls one endpoint as the signed-in user and returns the API's JSON as it is.
/// </summary>
[McpServerToolType]
public sealed class BudgetTools
{
  private const string BudgetsPath = "budgeting/budgets";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public BudgetTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET budgeting/budgets/list/{count}
  /// </summary>
  [McpServerTool(Name = "list_budgets", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List the signed-in user's most recent monthly budgets, newest first. Each entry gives the budget's name (description), year, month, whether it is closed, and its income, spending and remaining totals. "
      + "Use this to find which months have a budget before calling get_budget. Amounts are in the user's own currency."
  )]
  public Task<string> ListBudgets([Description("How many budgets to return, from 1 to 120. Defaults to 12 (about a year).")] int count = 12, CancellationToken cancellationToken = default)
  {
    ToolArguments.RequireRange(nameof(count), count, 1, 120);

    return _tivApi.GetJsonAsync($"{BudgetsPath}/list/{count}", cancellationToken: cancellationToken);
  }

  /// <summary>
  /// GET budgeting/budgets/{description}/{year}/{month}
  /// </summary>
  [McpServerTool(Name = "get_budget", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get one month's budget in full: its totals, every category (budgetCategories), every budget item (budgetItems) with the amount budgeted, spent and remaining, and every actual (budgetActuals) under each item. "
      + "An actual is one amount recorded against an item, such as a purchase or a paycheck, with its date in relevantOn. "
      + "Categories also carry categoryPredicted when the user has set a spending prediction. "
      + "If the user has no budget for that month, the result is an empty budget with no categories and isNew set to true; nothing is created. "
      + "A budget covers one calendar month, so call this once per month to cover a longer period. The result can be large."
  )]
  public Task<string> GetBudget(
    [Description("The four-digit year of the budget, for example 2026.")] int year,
    [Description("The month of the budget, 1 (January) to 12 (December).")] int month,
    [Description("The budget's name. Leave empty for the user's main budget, which is named 'default'.")] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);

    return _tivApi.GetJsonAsync($"{BudgetsPath}/{Uri.EscapeDataString(description)}/{year}/{month}", cancellationToken: cancellationToken);
  }
}
