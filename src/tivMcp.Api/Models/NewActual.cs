using System.ComponentModel;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// One transaction to record with the add_actuals tool. The same facts as the add_actual tool's
/// arguments, without the year, month and budget name, which are given once for the whole list.
/// </summary>
public class NewActual
{
  /// <summary>
  /// The name of the budget item the transaction goes under.
  /// </summary>
  [JsonPropertyName("itemDescription")]
  [Description("The name of the budget item, for example 'Groceries'.")]
  public string ItemDescription { get; set; }

  /// <summary>
  /// The transaction amount.
  /// </summary>
  [JsonPropertyName("amount")]
  [Description("The transaction amount, as a positive number.")]
  public decimal Amount { get; set; }

  /// <summary>
  /// What the transaction was.
  /// </summary>
  [JsonPropertyName("description")]
  [Description("What the transaction was, for example the shop's name.")]
  public string Description { get; set; }

  /// <summary>
  /// The transaction date, written as YYYY-MM-DD.
  /// </summary>
  [JsonPropertyName("date")]
  [Description("The transaction date written as YYYY-MM-DD. It should fall in the budget's month.")]
  public string Date { get; set; }

  /// <summary>
  /// The category the item is in, when the item's name alone is not enough.
  /// </summary>
  [JsonPropertyName("categoryDescription")]
  [Description("The category the item is in. Only needed when two categories have an item with the same name.")]
  public string? CategoryDescription { get; set; }

  /// <summary>
  /// True when the purchase was paid with a credit account.
  /// </summary>
  [JsonPropertyName("isCreditWithdrawl")]
  [Description("True when the purchase was paid with a credit account.")]
  public bool IsCreditWithdrawl { get; set; }

  /// <summary>
  /// The account to link the transaction to.
  /// </summary>
  [JsonPropertyName("accountLinkId")]
  [Description("The full 36-character id of the account to link the transaction to. See add_actual for when it is needed.")]
  public string? AccountLinkId { get; set; }

  /// <summary>
  /// The account category to link the transaction to.
  /// </summary>
  [JsonPropertyName("accountCategoryLinkId")]
  [Description("The full 36-character id of the account category to link the transaction to. Needed whenever accountLinkId is given.")]
  public string? AccountCategoryLinkId { get; set; }
}
