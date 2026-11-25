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
  private const string ApplyToolsPath = "budget-agent/apply-tools";

  private const string NothingSaved = "Nothing was saved.";
  private const string SaveOutcomeUnknown = "The save may or may not have gone through. Read the budget again before retrying, so nothing is added twice.";

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
  public Task<string> ApplyAndSaveAsync(int year, int month, string? budgetName, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    return SaveAsync(ApplyAsync(year, month, budgetName, [new ToolCall(tool, arguments)], cancellationToken), cancellationToken);
  }

  /// <inheritdoc />
  public Task<string> ApplyAllAndSaveAsync(int year, int month, string? budgetName, IReadOnlyList<ToolCall> calls, CancellationToken cancellationToken = default)
  {
    if (calls == null || calls.Count == 0)
    {
      throw new McpException("There is nothing to apply.");
    }

    return SaveAsync(ApplyAsync(year, month, budgetName, calls, cancellationToken), cancellationToken);
  }

  private async Task<string> SaveAsync(Task<ApplyToolResult> applying, CancellationToken cancellationToken)
  {
    var result = await applying;

    if (result.Budget.ValueKind != JsonValueKind.Object)
    {
      throw new McpException("trackItsValue returned an edited budget this server could not read. Nothing was saved.");
    }

    // The same call the website makes when the user presses Save: the whole budget, with the
    // new, changed and deleted parts flagged by the agent API.
    try
    {
      await _tivApi.PutJsonAsync(BudgetsPath, result.Budget.GetRawText(), cancellationToken);
    }
    catch (TivCallFailedException failure)
    {
      // A refusal means nothing was written. No answer, or a fault part-way, leaves it unknown,
      // and retrying blind could add the same thing twice.
      throw failure.During("Saving the budget failed.", failure.OutcomeUnknown ? SaveOutcomeUnknown : NothingSaved);
    }

    var changes = (result.Changes ?? []).Select(change => change?.Description).Where(description => !string.IsNullOrWhiteSpace(description)).ToList();
    return changes.Count > 0 ? "Saved. " + string.Join('\n', changes) : "Saved.";
  }

  /// <inheritdoc />
  public async Task<string> QueryAsync(int year, int month, string? budgetName, string tool, CancellationToken cancellationToken = default)
  {
    var result = await ApplyAsync(year, month, budgetName, [new ToolCall(tool, NoArguments)], cancellationToken);

    return result.Data.ValueKind == JsonValueKind.Undefined ? "{}" : result.Data.GetRawText();
  }

  private async Task<ApplyToolResult> ApplyAsync(int year, int month, string? budgetName, IReadOnlyList<ToolCall> calls, CancellationToken cancellationToken)
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);
    var description = ToolArguments.BudgetDescriptionOrDefault(budgetName);

    // The website sends these same three things to the agent API with every edit: the budget,
    // the month's accounts (for linking purchases to an account) and the budget templates.
    var budgetTask = Step(
      $"Reading the {year}-{month:00} budget failed.",
      _tivApi.GetJsonAsync($"{BudgetsPath}/{Uri.EscapeDataString(description)}/{year}/{month}", cancellationToken: cancellationToken)
    );
    var accountsTask = Step($"Reading the {year}-{month:00} accounts failed.", _tivApi.GetJsonAsync($"accounting/accounts/all/{year}/{month}", cancellationToken: cancellationToken));
    var templatesTask = Step("Reading the budget templates failed.", _tivApi.GetJsonAsync("budgeting/templates", cancellationToken: cancellationToken));
    await Task.WhenAll(budgetTask, accountsTask, templatesTask);

    var budgetJson = await budgetTask;
    if (Read<BudgetState>(budgetJson) is not { IsNew: false })
    {
      throw new McpException($"There is no budget for {year}-{month:00} yet. Create it in trackItsValue first.");
    }

    // One tool goes to the route for one, several to the route that applies them all or none.
    var path = calls.Count == 1 ? ApplyToolPath : ApplyToolsPath;
    var response = await Step("Working out the change failed.", _tivAgentApi.PostJsonAsync(path, BuildRequest(calls, budgetJson, await accountsTask, await templatesTask), cancellationToken));

    var result = Read<ApplyToolResult>(response) ?? throw new McpException("trackItsValue returned an answer this server could not read. Nothing was saved.");
    if (!result.Success)
    {
      throw new McpException(string.IsNullOrWhiteSpace(result.Error) ? "trackItsValue could not make that change. Nothing was saved." : $"{result.Error} Nothing was saved.");
    }

    return result;
  }

  /// <summary>
  /// Runs one step that comes before the save. If the service it calls fails, the error says
  /// which step that was, and that nothing was saved.
  /// </summary>
  private static async Task<string> Step(string step, Task<string> call)
  {
    try
    {
      return await call;
    }
    catch (TivCallFailedException failure)
    {
      throw failure.During(step, NothingSaved);
    }
  }

  /// <summary>
  /// Writes the apply-tool request for one call, or the apply-tools request for several. The
  /// API's JSON is placed in it as it is, without being read.
  /// </summary>
  private static string BuildRequest(IReadOnlyList<ToolCall> calls, string budgetJson, string accountsJson, string templatesJson)
  {
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      writer.WriteStartObject();

      if (calls.Count == 1)
      {
        WriteCall(writer, calls[0]);
      }
      else
      {
        writer.WriteStartArray("calls");
        foreach (var call in calls)
        {
          writer.WriteStartObject();
          WriteCall(writer, call);
          writer.WriteEndObject();
        }
        writer.WriteEndArray();
      }

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

  /// <summary>
  /// Writes a call's tool name and its arguments, leaving out arguments that were not given.
  /// </summary>
  private static void WriteCall(Utf8JsonWriter writer, ToolCall call)
  {
    writer.WriteString("tool", call.Tool);

    writer.WriteStartObject("arguments");
    foreach (var argument in call.Arguments.Where(argument => argument.Value != null))
    {
      writer.WritePropertyName(argument.Key);
      JsonSerializer.Serialize(writer, argument.Value, argument.Value!.GetType());
    }
    writer.WriteEndObject();
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
