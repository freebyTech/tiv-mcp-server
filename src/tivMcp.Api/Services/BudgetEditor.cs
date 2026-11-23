using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Services;

/// <inheritdoc />
public class BudgetEditor : IBudgetEditor
{
  private const string BudgetsPath = "budgeting/budgets";
  private const string ApplyToolPath = "budget-agent/apply-tool";

  private static readonly IReadOnlyDictionary<string, object?> NoArguments = new Dictionary<string, object?>();

  private readonly ITivApiClient _tivApi;
  private readonly ITivAgentApiClient _tivAgentApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public BudgetEditor(ITivApiClient tivApi, ITivAgentApiClient tivAgentApi)
  {
    _tivApi = tivApi;
    _tivAgentApi = tivAgentApi;
  }

  /// <inheritdoc />
  public async Task<string> ApplyAndSaveAsync(int year, int month, string? budgetName, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    var result = await ApplyAsync(year, month, budgetName, tool, arguments, cancellationToken);

    if (result.Budget.ValueKind != JsonValueKind.Object)
    {
      throw new McpException("trackItsValue returned an edited budget this server could not read. Nothing was saved.");
    }

    // The same call the website makes when the user presses Save: the whole budget, with the
    // new, changed and deleted parts flagged by the agent API.
    await _tivApi.PutJsonAsync(BudgetsPath, result.Budget.GetRawText(), cancellationToken);

    var changes = (result.Changes ?? []).Select(change => change?.Description).Where(description => !string.IsNullOrWhiteSpace(description)).ToList();
    return changes.Count > 0 ? "Saved. " + string.Join('\n', changes) : "Saved.";
  }

  /// <inheritdoc />
  public async Task<string> QueryAsync(int year, int month, string? budgetName, string tool, CancellationToken cancellationToken = default)
  {
    var result = await ApplyAsync(year, month, budgetName, tool, NoArguments, cancellationToken);

    return result.Data.ValueKind == JsonValueKind.Undefined ? "{}" : result.Data.GetRawText();
  }

  private async Task<ApplyToolResult> ApplyAsync(int year, int month, string? budgetName, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);

    // The website sends these same three things to the agent API with every edit: the budget,
    // the month's accounts (for linking purchases to an account) and the budget templates.
    var budgetTask = _tivApi.GetJsonAsync($"{BudgetsPath}/{Uri.EscapeDataString(description)}/{year}/{month}", cancellationToken: cancellationToken);
    var accountsTask = _tivApi.GetJsonAsync($"accounting/accounts/all/{year}/{month}", cancellationToken: cancellationToken);
    var templatesTask = _tivApi.GetJsonAsync("budgeting/templates", cancellationToken: cancellationToken);
    await Task.WhenAll(budgetTask, accountsTask, templatesTask);

    var budgetJson = await budgetTask;
    if (Read<BudgetState>(budgetJson) is not { IsNew: false })
    {
      throw new McpException($"There is no budget for {year}-{month:00} yet. Create it in trackItsValue first.");
    }

    var response = await _tivAgentApi.PostJsonAsync(ApplyToolPath, BuildRequest(tool, arguments, budgetJson, await accountsTask, await templatesTask), cancellationToken);

    var result = Read<ApplyToolResult>(response) ?? throw new McpException("trackItsValue returned an answer this server could not read. Nothing was saved.");
    if (!result.Success)
    {
      throw new McpException(string.IsNullOrWhiteSpace(result.Error) ? "trackItsValue could not make that change. Nothing was saved." : $"{result.Error} Nothing was saved.");
    }

    return result;
  }

  /// <summary>
  /// Writes the apply-tool request. The API's JSON is placed in it as it is, without being read.
  /// </summary>
  private static string BuildRequest(string tool, IReadOnlyDictionary<string, object?> arguments, string budgetJson, string accountsJson, string templatesJson)
  {
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      writer.WriteStartObject();
      writer.WriteString("tool", tool);

      writer.WriteStartObject("arguments");
      foreach (var argument in arguments.Where(argument => argument.Value != null))
      {
        writer.WritePropertyName(argument.Key);
        JsonSerializer.Serialize(writer, argument.Value, argument.Value!.GetType());
      }
      writer.WriteEndObject();

      writer.WriteStartObject("context");
      writer.WritePropertyName("budget");
      writer.WriteRawValue(budgetJson);
      WriteOptional(writer, "account-overview", accountsJson, '{');
      WriteOptional(writer, "budget-templates", templatesJson, '[');
      writer.WriteEndObject();

      writer.WriteEndObject();
    }
    return Encoding.UTF8.GetString(stream.ToArray());
  }

  private static void WriteOptional(Utf8JsonWriter writer, string name, string json, char expectedStart)
  {
    if (!string.IsNullOrWhiteSpace(json) && json.TrimStart()[0] == expectedStart)
    {
      writer.WritePropertyName(name);
      writer.WriteRawValue(json);
    }
  }

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
