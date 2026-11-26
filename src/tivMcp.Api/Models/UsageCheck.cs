using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The trackItsValue API's answer to a usage check (GET usage/check): whether the user may make
/// another call against a monthly allowance, and the numbers behind that decision.
/// </summary>
public class UsageCheck
{
  /// <summary>
  /// Whether another call is allowed. Stays true when the API leaves it out.
  /// </summary>
  [JsonPropertyName("allowed")]
  public bool Allowed { get; set; } = true;

  /// <summary>
  /// The user's plan, for example "free" or "paid".
  /// </summary>
  [JsonPropertyName("tier")]
  public string? Tier { get; set; }

  /// <summary>
  /// When the count starts again, in UTC.
  /// </summary>
  [JsonPropertyName("resetAtUtc")]
  public DateTime? ResetAtUtc { get; set; }

  /// <summary>
  /// What is counted, each with how much is used and its limit.
  /// </summary>
  [JsonPropertyName("dimensions")]
  public List<UsageDimension>? Dimensions { get; set; }
}

/// <summary>
/// One counted quantity in a <see cref="UsageCheck"/>.
/// </summary>
public class UsageDimension
{
  /// <summary>
  /// What is counted; "calls" for tool calls.
  /// </summary>
  [JsonPropertyName("name")]
  public string? Name { get; set; }

  /// <summary>
  /// How much has been used this month.
  /// </summary>
  [JsonPropertyName("used")]
  public long Used { get; set; }

  /// <summary>
  /// The month's limit, when there is one.
  /// </summary>
  [JsonPropertyName("limit")]
  public long? Limit { get; set; }
}
