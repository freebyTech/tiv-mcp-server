using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The two facts this server reads from a budget: whether it exists yet, and its id.
/// </summary>
public class BudgetState
{
  /// <summary>
  /// The budget's id, needed to name it as the source of a copy.
  /// </summary>
  [JsonPropertyName("id")]
  public string Id { get; set; }

  /// <summary>
  /// True when the API answered with a blank, unsaved budget because the user has none for that month.
  /// </summary>
  [JsonPropertyName("isNew")]
  public bool IsNew { get; set; }
}
