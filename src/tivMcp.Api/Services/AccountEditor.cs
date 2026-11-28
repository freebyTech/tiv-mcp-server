using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Services;

/// <inheritdoc />
public class AccountEditor : IAccountEditor
{
  private const string AccountsPath = "accounting/accounts";
  private const string ApplyPath = "budget-agent/apply-account-tools";

  private const string NothingSaved = "Nothing was saved.";
  private const string SaveOutcomeUnknown = "The save may or may not have gone through. Read the accounts again before retrying, so nothing is added twice.";

  private readonly ITivApiClient _tivApi;
  private readonly ITivAgentApiClient _tivAgentApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public AccountEditor(ITivApiClient tivApi, ITivAgentApiClient tivAgentApi)
  {
    _tivApi = tivApi;
    _tivAgentApi = tivAgentApi;
  }

  /// <inheritdoc />
  public async Task<string> ApplyAndSaveAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    var result = await ApplyAsync(year, month, tool, arguments, cancellationToken);

    if (result.Accounts.ValueKind != JsonValueKind.Object)
    {
      throw new McpException("trackItsValue returned edited accounts this server could not read. Nothing was saved.");
    }

    // The same call the website's account page makes when the user presses Save: the whole
    // month's accounts, with the new, changed and deleted parts flagged by the agent API.
    try
    {
      await _tivApi.PutJsonAsync(AccountsPath, result.Accounts.GetRawText(), cancellationToken);
    }
    catch (TivCallFailedException failure)
    {
      throw failure.During("Saving the accounts failed.", failure.OutcomeUnknown ? SaveOutcomeUnknown : NothingSaved);
    }

    var changes = (result.Changes ?? []).Select(change => change?.Description).Where(description => !string.IsNullOrWhiteSpace(description)).ToList();
    return changes.Count > 0 ? "Saved. " + string.Join('\n', changes) : "Saved.";
  }

  /// <inheritdoc />
  public async Task<JsonElement> QueryAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    var result = await ApplyAsync(year, month, tool, arguments, cancellationToken);

    return result.Data;
  }

  private async Task<ApplyAccountToolsResult> ApplyAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
  {
    ToolArguments.RequireYear(year);
    ToolArguments.RequireMonth(month);

    // What the website's account page loads: the month's accounts, and the templates and
    // account types that say which categories and kinds of transaction an account can have.
    var accountsTask = Step($"Reading the {year}-{month:00} accounts failed.", _tivApi.GetJsonAsync($"{AccountsPath}/all/{year}/{month}", cancellationToken: cancellationToken));
    var templatesTask = Step("Reading the account templates failed.", _tivApi.GetJsonAsync("accounting/templates", cancellationToken: cancellationToken));
    var typesTask = Step("Reading the account types failed.", _tivApi.GetJsonAsync($"{AccountsPath}/allAccountTypes", cancellationToken: cancellationToken));
    await Task.WhenAll(accountsTask, templatesTask, typesTask);

    var body = BuildRequest(new ToolCall(tool, arguments), await accountsTask, await templatesTask, await typesTask);
    var response = await Step("Working out the change failed.", _tivAgentApi.PostJsonAsync(ApplyPath, body, cancellationToken));

    var result = Read(response) ?? throw new McpException("trackItsValue returned an answer this server could not read. Nothing was saved.");
    if (!result.Success)
    {
      throw new McpException(string.IsNullOrWhiteSpace(result.Error) ? "trackItsValue could not make that change. Nothing was saved." : $"{result.Error} Nothing was saved.");
    }

    return result;
  }

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
  /// Builds the agent API request. The API's JSON is written into it as it was received, so no
  /// field is lost or reformatted on the way.
  /// </summary>
  private static string BuildRequest(ToolCall call, string accountsJson, string templatesJson, string typesJson)
  {
    if (string.IsNullOrWhiteSpace(accountsJson) || accountsJson.TrimStart()[0] != '{')
    {
      throw new McpException("trackItsValue returned accounts this server could not read. Nothing was saved.");
    }

    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      writer.WriteStartObject();

      writer.WriteStartArray("calls");
      writer.WriteStartObject();
      writer.WriteString("tool", call.Tool);
      writer.WriteStartObject("arguments");
      foreach (var argument in call.Arguments.Where(argument => argument.Value != null))
      {
        writer.WritePropertyName(argument.Key);
        JsonSerializer.Serialize(writer, argument.Value, argument.Value!.GetType());
      }
      writer.WriteEndObject();
      writer.WriteEndObject();
      writer.WriteEndArray();

      writer.WriteStartObject("context");
      writer.WritePropertyName("accounts");
      writer.WriteRawValue(accountsJson);
      WriteList(writer, "account-templates", templatesJson);
      WriteList(writer, "account-types", typesJson);
      writer.WriteEndObject();

      writer.WriteEndObject();
    }
    return Encoding.UTF8.GetString(stream.ToArray());
  }

  private static void WriteList(Utf8JsonWriter writer, string name, string json)
  {
    if (!string.IsNullOrWhiteSpace(json) && json.TrimStart()[0] == '[')
    {
      writer.WritePropertyName(name);
      writer.WriteRawValue(json);
    }
  }

  private static ApplyAccountToolsResult? Read(string json)
  {
    if (string.IsNullOrWhiteSpace(json))
    {
      return null;
    }

    try
    {
      return JsonSerializer.Deserialize<ApplyAccountToolsResult>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
