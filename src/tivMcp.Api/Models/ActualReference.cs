using System.ComponentModel;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// Names one transaction that is already in a budget, for the combine_actuals and
/// separate_actuals tools. The same facts update_actual and delete_actual use to find one.
/// </summary>
public class ActualReference
{
  /// <summary>
  /// The name of the budget item the transaction is under.
  /// </summary>
  [JsonPropertyName("itemDescription")]
  [Description("The name of the budget item the transaction is under, for example 'Groceries'.")]
  public string ItemDescription { get; set; }

  /// <summary>
  /// The transaction's description.
  /// </summary>
  [JsonPropertyName("actualDescription")]
  [Description("The transaction's description.")]
  public string ActualDescription { get; set; }

  /// <summary>
  /// The category the item is in, when the item's name alone is not enough.
  /// </summary>
  [JsonPropertyName("categoryDescription")]
  [Description("The category the item is in. Only needed when two categories have an item with the same name.")]
  public string? CategoryDescription { get; set; }

  /// <summary>
  /// The transaction's date, written as YYYY-MM-DD.
  /// </summary>
  [JsonPropertyName("actualDate")]
  [Description("The transaction's date written as YYYY-MM-DD, to tell apart transactions with the same description.")]
  public string? ActualDate { get; set; }

  /// <summary>
  /// The transaction's amount.
  /// </summary>
  [JsonPropertyName("actualAmount")]
  [Description("The transaction's amount, to tell apart transactions with the same description.")]
  public decimal? ActualAmount { get; set; }
}
