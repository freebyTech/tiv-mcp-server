using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The body of the trackItsValue API's verify-transactions request
/// (POST accounting/accounts/verify-transactions).
/// </summary>
public class VerifyAccountTransactionsRequest
{
  /// <summary>
  /// The account's id, when the account was given as one.
  /// </summary>
  [JsonPropertyName("accountId")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? AccountId { get; set; }

  /// <summary>
  /// The account's name, when the account was given by name.
  /// </summary>
  [JsonPropertyName("accountName")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? AccountName { get; set; }

  /// <summary>
  /// The year of the month to work on.
  /// </summary>
  [JsonPropertyName("year")]
  public int Year { get; set; }

  /// <summary>
  /// The month to work on, 1 to 12.
  /// </summary>
  [JsonPropertyName("month")]
  public int Month { get; set; }

  /// <summary>
  /// The last day of the month to cover, 1 to 31; left out for the whole month.
  /// </summary>
  [JsonPropertyName("throughDay")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public int? ThroughDay { get; set; }

  /// <summary>
  /// True to mark transactions verified, false to mark them not verified.
  /// </summary>
  [JsonPropertyName("verified")]
  public bool Verified { get; set; }

  /// <summary>
  /// True to change nothing and only report what would change.
  /// </summary>
  [JsonPropertyName("dryRun")]
  public bool DryRun { get; set; }
}
