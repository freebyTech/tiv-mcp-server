using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The body of the trackItsValue API's copy-budget request (POST budgeting/budgets/copy).
/// </summary>
public class BudgetCopyRequest
{
  /// <summary>
  /// The id of the budget to copy from.
  /// </summary>
  [JsonPropertyName("sourceId")]
  public string SourceId { get; set; }

  /// <summary>
  /// The name of the budget to create.
  /// </summary>
  [JsonPropertyName("destinationDescription")]
  public string DestinationDescription { get; set; }

  /// <summary>
  /// The month of the budget to create, 1 to 12.
  /// </summary>
  [JsonPropertyName("destinationMonth")]
  public int DestinationMonth { get; set; }

  /// <summary>
  /// The year of the budget to create.
  /// </summary>
  [JsonPropertyName("destinationYear")]
  public int DestinationYear { get; set; }

  /// <summary>
  /// Whether the source budget's transactions are copied too.
  /// </summary>
  [JsonPropertyName("copyActuals")]
  public bool CopyActuals { get; set; }
}
