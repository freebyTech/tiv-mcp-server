using ModelContextProtocol;

namespace tivMcp.Api.Tools;

/// <summary>
/// Checks tool arguments before anything is sent to the API, so a bad argument gets a message that
/// names the argument instead of whatever the API would answer to a malformed URL. These are
/// shape checks only; what a user may see or do is always decided by the API.
/// </summary>
public static class ToolArguments
{
  /// <summary>The name of a user's main budget.</summary>
  public const string DefaultBudgetDescription = "default";

  /// <summary>Checks a calendar year.</summary>
  public static void RequireYear(int year)
  {
    if (year < 2000 || year > 2100)
    {
      throw new McpException($"year must be between 2000 and 2100, but was {year}.");
    }
  }

  /// <summary>Checks a calendar month.</summary>
  public static void RequireMonth(int month)
  {
    if (month < 1 || month > 12)
    {
      throw new McpException($"month must be between 1 (January) and 12 (December), but was {month}.");
    }
  }

  /// <summary>Checks a whole number is within a range.</summary>
  public static void RequireRange(string name, int value, int minimum, int maximum)
  {
    if (value < minimum || value > maximum)
    {
      throw new McpException($"{name} must be between {minimum} and {maximum}, but was {value}.");
    }
  }

  /// <summary>Returns the budget name to use, falling back to the main budget.</summary>
  public static string BudgetDescriptionOrDefault(string? description)
  {
    var trimmed = description?.Trim();
    if (string.IsNullOrEmpty(trimmed))
    {
      return DefaultBudgetDescription;
    }
    if (trimmed.Length > 200 || trimmed.Contains('/') || trimmed.Contains('\\'))
    {
      throw new McpException("budgetName must be at most 200 characters and cannot contain a slash.");
    }
    return trimmed;
  }

  /// <summary>Checks a value is one of a fixed set, ignoring case, and returns the listed spelling.</summary>
  public static string RequireOneOf(string name, string? value, params string[] allowed)
  {
    var match = allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    if (match == null)
    {
      throw new McpException($"{name} must be one of: {string.Join(", ", allowed)}.");
    }
    return match;
  }
}
