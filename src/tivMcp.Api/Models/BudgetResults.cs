using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// What this server reads from the API's answer to closing a budget. The answer also carries the
/// whole budget, which is not read.
/// </summary>
public class BudgetCloseResult
{
  /// <summary>Experience points earned for closing the budget.</summary>
  [JsonPropertyName("closedXp")]
  public int ClosedXp { get; set; }

  /// <summary>Experience points earned for how closely spending matched the prediction.</summary>
  [JsonPropertyName("onTargetXp")]
  public int OnTargetXp { get; set; }
}

/// <summary>
/// What this server reads from the API's answer to reopening a budget.
/// </summary>
public class BudgetReopenResult
{
  /// <summary>Experience points taken back.</summary>
  [JsonPropertyName("xpDeducted")]
  public int XpDeducted { get; set; }
}
