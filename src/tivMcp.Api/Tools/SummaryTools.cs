using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tools that return a compact text summary in place of the API's full JSON. Each one
/// gets the JSON from the trackItsValue API, hands it as it is to the trackItsValue agent API,
/// which owns the conversion to the form an assistant reads, and returns the agent API's text.
/// </summary>
[McpServerToolType]
public sealed class SummaryTools
{
  private readonly ITivApiClient _tivApi;
  private readonly ITivAgentApiClient _tivAgentApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public SummaryTools(ITivApiClient tivApi, ITivAgentApiClient tivAgentApi)
  {
    _tivApi = tivApi;
    _tivAgentApi = tivAgentApi;
  }

  /// <summary>
  /// GET budgeting/budgets/{description}/{year}/{month}, then POST budget-agent/summarize-budget
  /// </summary>
  [McpServerTool(Name = "get_budget_summary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get a short text summary of one month's budget: income and spending totals, then every category and budget item with the amount budgeted and spent. "
      + "Prefer this over get_budget for everyday questions such as how much is left for an item this month; it is far smaller. "
      + "Use get_budget only when the full detail of every field is needed. "
      + "Transactions that are one line on the bank or card statement carry the same [combined:...] tag; see combine_actuals. "
      + "If the user has no budget for that month the summary shows zero totals and no categories. "
      + "Set includeTemplates to true before adding a category or an item, to also list the category and item templates they can be based on. "
      + "When the question is about one category or one item, give category or item to get only that part with the budget's totals: far smaller still. "
      + "Names match without regard to case and may be partial; if nothing matches, the answer lists the names that exist."
  )]
  public async Task<string> GetBudgetSummary(
    [Description("The four-digit year of the budget, for example 2026.")] int year,
    [Description("The month of the budget, 1 (January) to 12 (December).")] int month,
    [Description("The budget's name. Leave empty for the user's main budget, which is named 'default'.")] string? budgetName = null,
    [Description("True to also list the available category and item templates. Only needed before adding a category or an item.")] bool includeTemplates = false,
    [Description("Only summarize categories with this name, for example 'Food'. Leave empty for all categories.")] string? category = null,
    [Description("Only summarize budget items with this name, for example 'Restaurants'. Leave empty for all items.")] string? item = null,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);
    var categoryFilter = ToolArguments.OptionalText(nameof(category), category);
    var itemFilter = ToolArguments.OptionalText(nameof(item), item);

    var budgetJson = await _tivApi.GetJsonAsync($"budgeting/budgets/{Uri.EscapeDataString(description)}/{year}/{month}", cancellationToken: cancellationToken);
    var templatesJson = includeTemplates ? await _tivApi.GetJsonAsync("budgeting/templates", cancellationToken: cancellationToken) : null;

    var more = string.IsNullOrWhiteSpace(templatesJson) ? string.Empty : $",\"budget-templates\":{templatesJson}";
    if (categoryFilter != null)
    {
      more += $",\"category\":{JsonSerializer.Serialize(categoryFilter)}";
    }
    if (itemFilter != null)
    {
      more += $",\"item\":{JsonSerializer.Serialize(itemFilter)}";
    }

    return await Summarize("budget-agent/summarize-budget", "budget", budgetJson, more, cancellationToken);
  }

  /// <summary>
  /// GET accounting/accounts/all/{year}/{month}, then POST budget-agent/summarize-accounts
  /// </summary>
  [McpServerTool(Name = "get_accounts_summary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Get a short text summary of all of the signed-in user's accounts for one month: each account with its balances and the month's transactions. "
      + "Closed accounts that were at $0.00 all month with no transactions are left out and only counted in the last line; open accounts are always listed. "
      + "Prefer this over get_accounts_overview for everyday questions about balances; it is far smaller. "
      + "Use get_accounts_overview only when the full detail of every field is needed."
  )]
  public async Task<string> GetAccountsSummary(
    [Description("The four-digit year, for example 2026.")] int year,
    [Description("The month, 1 (January) to 12 (December).")] int month,
    CancellationToken cancellationToken = default
  )
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);

    var accountsJson = await _tivApi.GetJsonAsync($"accounting/accounts/all/{year}/{month}", cancellationToken: cancellationToken);

    // The short form. The template name and id on every account, category and transaction are
    // only of use for creating account transactions, which no tool here does. Compact leaves out
    // closed accounts that sat at $0.00 all month and writes an unchanged fund on one line.
    return await Summarize("budget-agent/summarize-accounts", "accounts", accountsJson, ",\"include_templates\":false,\"compact\":true", cancellationToken);
  }

  /// <summary>
  /// Sends the API's JSON, unchanged, to an agent API summarize route under the given property
  /// name and returns the summary as text. <paramref name="more"/> is written after it as it is:
  /// further properties of the request, each starting with a comma, or nothing.
  /// </summary>
  private async Task<string> Summarize(string agentPath, string propertyName, string json, string more, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(json))
    {
      throw new McpException("trackItsValue returned nothing to summarize.");
    }

    var response = await _tivAgentApi.PostJsonAsync(agentPath, $"{{\"{propertyName}\":{json}{more}}}", cancellationToken);

    SummaryResult? result;
    try
    {
      result = JsonSerializer.Deserialize<SummaryResult>(response);
    }
    catch (JsonException)
    {
      result = null;
    }

    if (result?.Summary == null)
    {
      throw new McpException("trackItsValue returned a summary this server could not read. Try again later.");
    }

    return string.Join('\n', result.Summary);
  }
}
