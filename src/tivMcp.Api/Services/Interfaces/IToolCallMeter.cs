namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// Counts tool calls against the user's monthly allowance. The trackItsValue API holds the count
/// and the limits and makes the decision; this only asks it and reports to it.
/// </summary>
public interface IToolCallMeter
{
  /// <summary>
  /// Asks the API whether the user may make another tool call.
  /// </summary>
  /// <returns>Null when the call may go ahead, otherwise the reason to give the assistant.</returns>
  Task<string?> RefusalAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Reports one finished tool call to the API. Never fails the tool call it reports.
  /// </summary>
  Task RecordAsync(CancellationToken cancellationToken = default);
}
