using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The one fact this server reads from a budget: whether it exists yet.
/// </summary>
public class BudgetState
{
  /// <summary>
  /// True when the API answered with a blank, unsaved budget because the user has none for that month.
  /// </summary>
  [JsonPropertyName("isNew")]
  public bool IsNew { get; set; }
}
