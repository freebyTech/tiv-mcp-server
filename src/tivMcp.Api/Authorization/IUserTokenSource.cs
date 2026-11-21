namespace tivMcp.Api.Authorization;

/// <summary>
/// The single place that decides which access token is sent to the trackItsValue API on behalf of
/// the current caller. Tools and API clients never read the Authorization header themselves, so
/// changing how the token is obtained (for example to a token exchange) touches only the
/// implementation of this interface.
/// </summary>
public interface IUserTokenSource
{
  /// <summary>
  /// Returns the access token (without the "Bearer " prefix) to present to the trackItsValue API
  /// for the current request.
  /// </summary>
  /// <exception cref="UserTokenUnavailableException">There is no authenticated caller.</exception>
  string GetApiAccessToken();
}
