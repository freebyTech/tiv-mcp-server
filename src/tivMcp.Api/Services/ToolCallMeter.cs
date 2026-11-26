using System.Globalization;
using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Models;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Services;

/// <summary>
/// Counts tool calls through the trackItsValue API's usage endpoints, as the signed-in user. The
/// limits live in the API's configuration, per plan; nothing about them is known here.
/// </summary>
public class ToolCallMeter : IToolCallMeter
{
  /// <summary>The name of the allowance tool calls count against in the API.</summary>
  public const string Bucket = "mcp-tool";

  private const string UsagePath = "usage";

  private readonly ITivApiClient _tivApi;
  private readonly ILogger<ToolCallMeter> _logger;

  /// <summary>
  /// Constructor.
  /// </summary>
  public ToolCallMeter(ITivApiClient tivApi, ILogger<ToolCallMeter> logger)
  {
    _tivApi = tivApi;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<string?> RefusalAsync(CancellationToken cancellationToken = default)
  {
    string json;
    try
    {
      json = await _tivApi.GetJsonAsync($"{UsagePath}/check", new Dictionary<string, string> { ["bucket"] = Bucket }, cancellationToken);
    }
    catch (McpException error)
    {
      // The tool goes ahead: if the API is refusing this user or is down, the tool's own call
      // says so with the right reason, which a failed usage check could only obscure.
      _logger.LogWarning("The tool call allowance could not be checked, so the call goes ahead. {Reason}", error.Message);
      return null;
    }

    var check = Read(json);
    return check is { Allowed: false } ? DescribeRefusal(check) : null;
  }

  /// <inheritdoc />
  public async Task RecordAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await _tivApi.PostJsonAsync($"{UsagePath}/record", JsonSerializer.Serialize(new UsageRecordRequest { Bucket = Bucket, Calls = 1 }), cancellationToken);
    }
    catch (McpException error)
    {
      // The tool call itself succeeded and its result must still reach the assistant.
      _logger.LogWarning("A tool call could not be counted against the allowance. {Reason}", error.Message);
    }
  }

  /// <summary>
  /// The sentence an assistant is given when the month's allowance is used up.
  /// </summary>
  public static string DescribeRefusal(UsageCheck check)
  {
    var calls = check.Dimensions?.FirstOrDefault(dimension => string.Equals(dimension.Name, "calls", StringComparison.OrdinalIgnoreCase));
    var plan = string.IsNullOrWhiteSpace(check.Tier) ? "the user's plan" : $"the {check.Tier.Trim()} plan";

    var used = calls?.Limit is { } limit
      ? $"This month's assistant tool calls are used up: {limit.ToString("N0", CultureInfo.InvariantCulture)} are included in {plan}."
      : $"This month's assistant tool calls included in {plan} are used up.";
    var reset = check.ResetAtUtc is { } resetAt ? $" The count starts again on {resetAt.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)} (UTC)." : " The count starts again next month.";
    var more = string.Equals(check.Tier?.Trim(), "free", StringComparison.OrdinalIgnoreCase) ? " A paid plan includes more." : string.Empty;

    return $"{used}{reset} Nothing was done, and retrying before then will not help.{more}";
  }

  private static UsageCheck? Read(string json)
  {
    if (string.IsNullOrWhiteSpace(json))
    {
      return null;
    }

    try
    {
      return JsonSerializer.Deserialize<UsageCheck>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
