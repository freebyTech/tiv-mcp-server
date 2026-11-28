using RestSharp;

namespace tivMcp.Api.Services;

/// <summary>
/// What both API clients do when a call fails: write the detail to the log, where it is safe, and
/// build the sentence for the user, which carries the reference that finds that log line.
/// </summary>
internal static class TivCallFailures
{
  /// <summary>The header the tool call's reference travels in.</summary>
  public const string ReferenceHeader = "X-Request-Id";

  /// <summary>The trackItsValue API, in words.</summary>
  public const string BudgetApi = "budget API";

  /// <summary>The trackItsValue agent API, in words.</summary>
  public const string AgentApi = "agent API";

  // Enough of a body to see what went wrong without filling the log with a whole budget.
  private const int LongestBodyLogged = 1000;

  public static TivCallFailedException Describe(ILogger logger, string service, string reference, Method method, string path, RestResponse response, long elapsedMilliseconds)
  {
    var statusCode = (int)response.StatusCode;
    var timedOut = response.ResponseStatus == ResponseStatus.TimedOut;

    // The body of a failed call can hold internal detail, so it goes to the log and not to the user.
    var detail = statusCode == 0 ? $"{response.ResponseStatus}: {response.ErrorMessage}" : Excerpt(response.Content);
    logger.LogWarning(
      "Call {Reference} to the {Service} failed: {Method} {Path} answered {StatusCode} after {Elapsed} ms. {Detail}",
      reference,
      service,
      method.ToString().ToUpperInvariant(),
      path,
      statusCode,
      elapsedMilliseconds,
      detail
    );

    var message = TivApiErrorMapper.Describe(statusCode, response.Content, timedOut);
    if (TivApiErrorMapper.IsUnexplained(statusCode, response.Content))
    {
      message += $" Reference {reference} ({service}).";
    }

    return new TivCallFailedException(message, statusCode, service, reference);
  }

  private static string Excerpt(string? body)
  {
    if (string.IsNullOrWhiteSpace(body))
    {
      return "(no body)";
    }

    var text = body.Trim();
    return text.Length > LongestBodyLogged ? text[..LongestBodyLogged] + "..." : text;
  }
}
