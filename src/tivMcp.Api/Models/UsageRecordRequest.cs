using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The body of the trackItsValue API's usage report (POST usage/record).
/// </summary>
public class UsageRecordRequest
{
  /// <summary>
  /// The allowance the usage counts against.
  /// </summary>
  [JsonPropertyName("bucket")]
  public string Bucket { get; set; } = string.Empty;

  /// <summary>
  /// How many calls to add.
  /// </summary>
  [JsonPropertyName("calls")]
  public long Calls { get; set; } = 1;
}
