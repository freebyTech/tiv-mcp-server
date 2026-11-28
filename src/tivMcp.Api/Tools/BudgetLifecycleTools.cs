using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Tools for the things the website does to a whole budget rather than to what is in it: setting
/// its prediction, closing and reopening it, and deleting it. Each finds the budget's id, makes
/// the one trackItsValue API call the website makes, and reports what the API answered. The API
/// decides whether the budget may be closed, reopened or predicted.
/// </summary>
[McpServerToolType]
public sealed class BudgetLifecycleTools
{
  private const string BudgetsPath = "budgeting/budgets";
  private const string RetryHint = " If it fails without a clear answer, check with get_budget_summary before retrying.";
  private const string YearHint = "The four-digit year of the budget, for example 2026.";
  private const string MonthHint = "The month of the budget, 1 (January) to 12 (December).";
  private const string BudgetNameHint = "The budget's name. Leave empty for the user's main budget, which is named 'default'.";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public BudgetLifecycleTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// POST budgeting/budgets/{id}/set-prediction
  /// </summary>
  [McpServerTool(Name = "set_budget_prediction", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Set the budget's prediction: a snapshot of what each category is budgeted now, which closing the budget later measures the month against ('on target' experience points). "
      + "Setting it again replaces the snapshot. The website only offers this in the first 5 days of the budget's month, when it also earns experience points once; "
      + "outside those days ask the user before using it. It is refused for a closed budget and for one with nothing budgeted."
      + RetryHint
  )]
  public async Task<string> SetBudgetPrediction(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var budget = await FindAsync(year, month, budgetName, cancellationToken);

    // The API answers with the whole budget, which is not passed on.
    await _tivApi.PostJsonAsync($"{BudgetsPath}/{budget.Id}/set-prediction", "{}", cancellationToken);

    return $"Set the prediction of the {year}-{month:00} budget to what each category is budgeted now.";
  }

  /// <summary>
  /// POST budgeting/budgets/{id}/close
  /// </summary>
  [McpServerTool(Name = "close_budget", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Close a month's budget: it becomes read-only and the user earns experience points, more when spending matched the prediction. Confirm with the user first. "
      + "A budget can be closed when every dollar is accounted for, meaning nothing is left over or overspent; the API refuses with the reason otherwise "
      + "(rectify_budget and move_budget help get there). "
      + "A budget at least 12 months old with only a little left over can be closed anyway by setting acknowledgeRemaining to true, which earns no points; "
      + "use that only when the user asks for it. reopen_budget undoes a close."
      + RetryHint
  )]
  public async Task<string> CloseBudget(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    [Description("True to close an old budget that still has a little left over or overspent. Defaults to false.")] bool acknowledgeRemaining = false,
    CancellationToken cancellationToken = default
  )
  {
    var budget = await FindAsync(year, month, budgetName, cancellationToken);

    var path = $"{BudgetsPath}/{budget.Id}/close" + (acknowledgeRemaining ? "?acknowledgeRemaining=true" : string.Empty);
    var result = Read<BudgetCloseResult>(await _tivApi.PostJsonAsync(path, "{}", cancellationToken));

    var points = (result?.ClosedXp ?? 0) + (result?.OnTargetXp ?? 0);
    var onTarget = result?.OnTargetXp > 0 ? $", {result.OnTargetXp} of them for being on target" : string.Empty;
    return $"Closed the {year}-{month:00} budget. It earned {points} experience points{onTarget}.";
  }

  /// <summary>
  /// POST budgeting/budgets/{id}/reopen
  /// </summary>
  [McpServerTool(Name = "reopen_budget", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Reopen a closed budget so it can be changed again. Confirm with the user first: the experience points earned by closing it are taken back. "
      + "If the month's accounts are closed too, reopen those first with reopen_account_month; the API refuses otherwise."
      + RetryHint
  )]
  public async Task<string> ReopenBudget(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var budget = await FindAsync(year, month, budgetName, cancellationToken);

    var result = Read<BudgetReopenResult>(await _tivApi.PostJsonAsync($"{BudgetsPath}/{budget.Id}/reopen", "{}", cancellationToken));

    return $"Reopened the {year}-{month:00} budget. {result?.XpDeducted ?? 0} experience points were taken back.";
  }

  /// <summary>
  /// DELETE budgeting/budgets
  /// </summary>
  [McpServerTool(Name = "delete_budget", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Delete a month's whole budget: every category, item and transaction in it, and the account entries those transactions made. This cannot be undone. "
      + "Only do it when the user has asked for exactly this and has confirmed it after being told what will be lost. "
      + "The API refuses to delete a closed budget; reopen it first with reopen_budget."
      + RetryHint
  )]
  public async Task<string> DeleteBudget(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var budget = await FindAsync(year, month, budgetName, cancellationToken);

    // The website sends the budget it has loaded as the body, and so does this.
    await _tivApi.DeleteJsonAsync(BudgetsPath, budget.Json, cancellationToken);

    return $"Deleted the {year}-{month:00} budget with everything in it.";
  }

  private async Task<FoundBudget> FindAsync(int year, int month, string? budgetName, CancellationToken cancellationToken)
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);

    var json = await _tivApi.GetJsonAsync($"{BudgetsPath}/{Uri.EscapeDataString(description)}/{year}/{month}", cancellationToken: cancellationToken);
    var state = Read<BudgetState>(json);
    if (state is not { IsNew: false } || string.IsNullOrWhiteSpace(state.Id))
    {
      throw new McpException($"There is no budget for {year}-{month:00}. list_budgets shows the user's budgets.");
    }

    return new FoundBudget(Uri.EscapeDataString(state.Id), json);
  }

  private sealed record FoundBudget(string Id, string Json);

  private static T? Read<T>(string json)
    where T : class
  {
    if (string.IsNullOrWhiteSpace(json))
    {
      return null;
    }

    try
    {
      return JsonSerializer.Deserialize<T>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
