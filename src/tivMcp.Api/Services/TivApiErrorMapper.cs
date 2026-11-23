using System.Text.Json;

namespace tivMcp.Api.Services;

/// <summary>
/// Turns a failed trackItsValue API call into one sentence an assistant can show the user. The
/// API stays the gatekeeper: nothing here decides whether a call is allowed, it only explains the
/// API's answer. Where the API gave a reason (its usual <c>{"StatusCode":403,"Message":"..."}</c>
/// body, or the agent API's <c>{"detail":"..."}</c>) that reason is passed on as written.
/// </summary>
public static class TivApiErrorMapper
{
  // Longer bodies are error pages or stack traces, not a message meant for a person.
  private const int LongestMessageShown = 400;

  /// <summary>
  /// Describes a failed call.
  /// </summary>
  /// <param name="statusCode">The HTTP status the API answered with, or 0 when there was no answer at all.</param>
  /// <param name="body">The response body, if any.</param>
  public static string Describe(int statusCode, string? body)
  {
    var reason = ExtractMessage(body);

    switch (statusCode)
    {
      case 0:
        return "trackItsValue could not be reached. Try again in a moment.";
      case 401:
        return "trackItsValue did not accept this sign-in. Disconnect and reconnect trackItsValue in your assistant, then try again.";
      case 402:
        return WithReason("This would go over the usage limit of your trackItsValue plan", reason);
      case 403:
        return reason != null ? $"trackItsValue refused this request: {reason}" : "trackItsValue refused this request. Your account, or the access granted to this assistant, does not allow it.";
      case 404:
        return WithReason("trackItsValue could not find what was asked for", reason);
      case 429:
        return "trackItsValue is receiving too many requests right now. Wait a minute and try again.";
    }

    if (statusCode >= 500)
    {
      // Server faults can carry internal detail; never pass their body on.
      return $"trackItsValue ran into a problem of its own (status {statusCode}). Try again later.";
    }

    return WithReason($"trackItsValue could not process this request (status {statusCode})", reason);
  }

  private static string WithReason(string sentence, string? reason) => reason != null ? $"{sentence}: {reason}" : $"{sentence}.";

  /// <summary>
  /// Pulls the human-readable message out of an API error body: the Message (or, from the agent
  /// API, detail) property of a JSON object, a bare JSON string, or short plain text. Returns null
  /// when there is nothing usable.
  /// </summary>
  internal static string? ExtractMessage(string? body)
  {
    if (string.IsNullOrWhiteSpace(body))
    {
      return null;
    }

    var text = body.Trim();
    string? message = null;

    if (text[0] is '{' or '"')
    {
      try
      {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.String)
        {
          message = root.GetString();
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
          foreach (var property in root.EnumerateObject())
          {
            var isMessage = string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase) || string.Equals(property.Name, "detail", StringComparison.OrdinalIgnoreCase);
            if (property.Value.ValueKind == JsonValueKind.String && isMessage)
            {
              message = property.Value.GetString();
              break;
            }
          }
        }
      }
      catch (JsonException)
      {
        return null;
      }
    }
    else if (text[0] != '<' && text[0] != '[')
    {
      message = text;
    }

    message = message?.Trim();
    return string.IsNullOrEmpty(message) || message.Length > LongestMessageShown ? null : message;
  }
}
