using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The agent API's answer to applying account tools: whether it worked, what changed, and the
/// edited accounts overview to save.
/// </summary>
public class ApplyAccountToolsResult
{
  /// <summary>Whether every tool did what was asked.</summary>
  [JsonPropertyName("success")]
  public bool Success { get; set; }

  /// <summary>The failing tool's own message.</summary>
  [JsonPropertyName("error")]
  public string Error { get; set; }

  /// <summary>What was changed.</summary>
  [JsonPropertyName("changes")]
  public List<ToolChange> Changes { get; set; }

  /// <summary>The edited accounts overview, passed on to the API as it is.</summary>
  [JsonPropertyName("accounts")]
  public JsonElement Accounts { get; set; }

  /// <summary>Anything else the last tool returned, for example a looked-up account.</summary>
  [JsonPropertyName("data")]
  public JsonElement Data { get; set; }
}

/// <summary>
/// Names one transaction of an account, for the tools that combine and separate them. The same
/// facts update_account_transaction uses to find one.
/// </summary>
public class AccountTransactionReference
{
  /// <summary>The transaction's description.</summary>
  [JsonPropertyName("transactionDescription")]
  [Description("The transaction's description as shown by get_accounts_summary.")]
  public string TransactionDescription { get; set; }

  /// <summary>The account category the transaction is in.</summary>
  [JsonPropertyName("category")]
  [Description("The account category the transaction is in, to narrow the search.")]
  public string? Category { get; set; }

  /// <summary>The transaction's date, written as YYYY-MM-DD.</summary>
  [JsonPropertyName("transactionDate")]
  [Description("The transaction's date written as YYYY-MM-DD, to tell apart transactions with the same description.")]
  public string? TransactionDate { get; set; }

  /// <summary>The transaction's amount.</summary>
  [JsonPropertyName("transactionAmount")]
  [Description("The transaction's amount, to tell apart transactions with the same description.")]
  public decimal? TransactionAmount { get; set; }
}

/// <summary>
/// What an account's balance should be on one date, for the set_account_balance tool.
/// </summary>
public class BalanceSnapshot
{
  /// <summary>The date the balance is known for, written as YYYY-MM-DD.</summary>
  [JsonPropertyName("date")]
  [Description("The date the balance is known for, written as YYYY-MM-DD.")]
  public string Date { get; set; }

  /// <summary>The balance on that date.</summary>
  [JsonPropertyName("targetBalance")]
  [Description("What the balance was at the end of that date. Negative for money owed.")]
  public decimal TargetBalance { get; set; }
}

/// <summary>
/// The body of POST accounting/accounts/{accountId}/set-balance-as-of.
/// </summary>
public class SetBalanceAsOfRequest
{
  /// <summary>The balances to set.</summary>
  [JsonPropertyName("snapshots")]
  public List<SetBalanceAsOfSnapshot> Snapshots { get; set; }

  /// <summary>False asks only for a preview of what would change.</summary>
  [JsonPropertyName("applyConfirmed")]
  public bool ApplyConfirmed { get; set; }

  /// <summary>Always false: a closed month is never reopened on the user's behalf.</summary>
  [JsonPropertyName("reopenClosedMonths")]
  public bool ReopenClosedMonths { get; set; }
}

/// <summary>
/// One balance of a <see cref="SetBalanceAsOfRequest"/>.
/// </summary>
public class SetBalanceAsOfSnapshot
{
  /// <summary>The account category the balance is for; left out for the account's default category.</summary>
  [JsonPropertyName("categoryId")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? CategoryId { get; set; }

  /// <summary>The date, written as YYYY-MM-DD.</summary>
  [JsonPropertyName("date")]
  public string Date { get; set; }

  /// <summary>The balance on that date.</summary>
  [JsonPropertyName("targetBalance")]
  public decimal TargetBalance { get; set; }
}

/// <summary>
/// The account the agent API found by name.
/// </summary>
public class FoundAccount
{
  /// <summary>The account's id.</summary>
  [JsonPropertyName("id")]
  public string Id { get; set; }

  /// <summary>The account's name.</summary>
  [JsonPropertyName("description")]
  public string Description { get; set; }

  /// <summary>The id of the category that was asked for with it, if one was.</summary>
  [JsonPropertyName("categoryId")]
  public string? CategoryId { get; set; }
}
