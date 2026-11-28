using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The body of POST budgeting/budgets/createNewBudget: the month to make a budget for and the
/// answers to build it from.
/// </summary>
public class BudgetCreateRequest
{
  /// <summary>The budget's name.</summary>
  [JsonPropertyName("description")]
  public string Description { get; set; }

  /// <summary>The budget's month, 1 to 12.</summary>
  [JsonPropertyName("month")]
  public int Month { get; set; }

  /// <summary>The budget's year.</summary>
  [JsonPropertyName("year")]
  public int Year { get; set; }

  /// <summary>Not used: the API works the estimate out from the answers.</summary>
  [JsonPropertyName("estimatedIncome")]
  public decimal EstimatedIncome { get; set; }

  /// <summary>Not used: the API works the estimate out from the answers.</summary>
  [JsonPropertyName("estimatedSpending")]
  public decimal EstimatedSpending { get; set; }

  /// <summary>The answers to the wizard's questions.</summary>
  [JsonPropertyName("budgetWizardQuestion")]
  public BudgetQuestionAnswers BudgetWizardQuestion { get; set; }
}
