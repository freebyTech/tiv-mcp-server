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
  /// True when this is cash taken out to spend on the item.
  /// </summary>
  [JsonPropertyName("isCashWithdrawal")]
  [Description(CashWithdrawalHint)]
  public bool IsCashWithdrawal { get; set; }

  /// <summary>
  /// What an assistant is told about isCashWithdrawal.
  /// </summary>
  internal const string CashWithdrawalHint =
    "True when this is cash taken out to spend on the item (the website's Cash Withdrawal). The item's type must allow cash. "
    + "To put the cash in a cash envelope account give that account's ids; without them it is simply recorded as spent.";

  /// <summary>
  /// What an assistant is told about accountLinkId, on add_actual and on each add_actuals entry.
  /// </summary>
  internal const string AccountLinkIdHint =
    "The full 36-character id of a different account than the default to link the transaction to. Leave empty to use the item's default account or the budget's own account.";

  /// <summary>
  /// What an assistant is told about accountCategoryLinkId.
  /// </summary>
  internal const string AccountCategoryLinkIdHint =
    "The full 36-character id of the account category to link the transaction to. Leave empty when accountLinkId is empty; needed whenever accountLinkId is given.";

  /// <summary>
  /// The account to link the transaction to.
  /// </summary>
  [JsonPropertyName("accountLinkId")]
  [Description(AccountLinkIdHint)]
  public string? AccountLinkId { get; set; }

  /// <summary>
  /// The account category to link the transaction to.
  /// </summary>
  [JsonPropertyName("accountCategoryLinkId")]
  [Description(AccountCategoryLinkIdHint)]
  public string? AccountCategoryLinkId { get; set; }
}
