namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// A short id for the tool call being served. It is sent to the trackItsValue services with every
/// request this call makes, written to this server's log when one of them fails, and shown to the
/// user in the error, so a reported failure can be matched to the log lines that explain it.
/// </summary>
public interface ICallReference
{
  /// <summary>
  /// The id: eight lower-case hex characters, the same for the whole tool call.
  /// </summary>
  string Value { get; }
}
