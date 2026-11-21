namespace tivMcp.Api.Authorization;

/// <summary>
/// Thrown when a call to the trackItsValue API is attempted without an authenticated caller.
/// </summary>
public class UserTokenUnavailableException : Exception
{
  /// <summary>
  /// Constructor.
  /// </summary>
  public UserTokenUnavailableException(string message)
    : base(message) { }
}
