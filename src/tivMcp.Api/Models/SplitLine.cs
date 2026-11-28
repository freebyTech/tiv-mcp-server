using System.ComponentModel;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// One part of a transaction being split with the split_actual tool: the amount that moves to
/// another budget item.
/// </summary>
public class SplitLine
{
  /// <summary>
  /// The name of the budget item this part goes to.
  /// </summary>
  [JsonPropertyName("itemDescription")]
  [Description("The name of the budget item this part goes to, for example 'Medicine'. It must be a different item than the transaction is on.")]
  public string ItemDescription { get; set; }

  /// <summary>
  /// The amount of this part.
  /// </summary>
  [JsonPropertyName("amount")]
  [Description("The amount of this part, as a positive number.")]
  public decimal Amount { get; set; }

  /// <summary>
  /// What this part was.
  /// </summary>
  [JsonPropertyName("description")]
  [Description("A description for this part. Leave empty to use the original transaction's description.")]
  public string? Description { get; set; }

  /// <summary>
  /// The category the item is in, when the item's name alone is not enough.
  /// </summary>
  [JsonPropertyName("categoryDescription")]
  [Description("The category the item is in. Only needed when two categories have an item with the same name.")]
  public string? CategoryDescription { get; set; }
}
