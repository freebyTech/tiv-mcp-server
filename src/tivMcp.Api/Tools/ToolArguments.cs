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

  /// <summary>Checks a piece of text was given, and returns it trimmed.</summary>
  public static string RequireText(string name, string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed))
    {
      throw new McpException($"{name} is required.");
    }
    if (trimmed.Length > 500)
    {
      throw new McpException($"{name} must be at most 500 characters.");
    }
    return trimmed;
  }

  /// <summary>Trims optional text, treating blank as not given.</summary>
  public static string? OptionalText(string name, string? value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : RequireText(name, value);
  }

  /// <summary>Checks a date is a real calendar date written as YYYY-MM-DD, and returns it in that form.</summary>
  public static string RequireDate(string name, string? value)
  {
    if (!DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
    {
      throw new McpException($"{name} must be a date written as YYYY-MM-DD, for example 2026-03-15.");
    }
    return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
  }

  /// <summary>Checks an optional date, treating blank as not given.</summary>
  public static string? OptionalDate(string name, string? value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : RequireDate(name, value);
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
