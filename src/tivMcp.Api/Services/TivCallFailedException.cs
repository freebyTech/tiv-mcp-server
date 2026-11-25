using ModelContextProtocol;

namespace tivMcp.Api.Services;

/// <summary>
/// A call to one of the trackItsValue services failed. The message is the sentence shown to the
/// user; the rest says which service failed and how, so a caller that knows what step it was on
/// can add that to the sentence.
/// </summary>
public sealed class TivCallFailedException : McpException
{
  /// <summary>
  /// Constructor.
  /// </summary>
  /// <param name="message">The sentence shown to the user.</param>
  /// <param name="statusCode">The HTTP status the service answered with, or 0 when it did not answer.</param>
  /// <param name="service">The service that was called, in words.</param>
  /// <param name="reference">The reference of the tool call, as written to the log.</param>
  public TivCallFailedException(string message, int statusCode, string service, string reference)
    : base(message)
  {
    StatusCode = statusCode;
    Service = service;
    Reference = reference;
  }

  /// <summary>The HTTP status the service answered with, or 0 when it did not answer.</summary>
  public int StatusCode { get; }

  /// <summary>The service that was called, in words.</summary>
  public string Service { get; }

  /// <summary>The reference of the tool call, as written to the log.</summary>
  public string Reference { get; }

  /// <summary>
  /// Whether the service may have done the work even though the call failed: it did not answer,
  /// or it failed part-way. A refusal (a 4xx) means it did nothing.
  /// </summary>
  public bool OutcomeUnknown => StatusCode == 0 || StatusCode >= 500;

  /// <summary>
  /// The same failure, with what was being done put before the sentence and what it means for
  /// the user's data put after.
  /// </summary>
  public TivCallFailedException During(string step, string consequence) => new($"{step} {Message} {consequence}", StatusCode, Service, Reference);
}
