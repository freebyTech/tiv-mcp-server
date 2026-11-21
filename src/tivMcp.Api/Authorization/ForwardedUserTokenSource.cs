using Microsoft.Net.Http.Headers;

namespace tivMcp.Api.Authorization;

/// <summary>
/// Forwards the caller's own access token. The token was issued for this MCP server's audience and
/// has already been validated by the JWT bearer handler; the trackItsValue API is configured to
/// accept that audience as well, so it resolves the same user and applies its own level and
/// privilege rules.
/// </summary>
public class ForwardedUserTokenSource : IUserTokenSource
{
  private const string BearerPrefix = "Bearer ";

  private readonly IHttpContextAccessor _httpContextAccessor;

  /// <summary>
  /// Constructor.
  /// </summary>
  public ForwardedUserTokenSource(IHttpContextAccessor httpContextAccessor)
  {
    _httpContextAccessor = httpContextAccessor;
  }

  /// <inheritdoc />
  public string GetApiAccessToken()
  {
    var httpContext = _httpContextAccessor.HttpContext;
    if (httpContext == null)
    {
      throw new UserTokenUnavailableException("There is no current request to take the caller's access token from.");
    }

    if (httpContext.User?.Identity?.IsAuthenticated != true)
    {
      throw new UserTokenUnavailableException("The current request is not authenticated.");
    }

    var authorization = httpContext.Request.Headers[HeaderNames.Authorization].ToString();
    if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      throw new UserTokenUnavailableException("The current request does not carry a bearer token.");
    }

    var token = authorization.Substring(BearerPrefix.Length).Trim();
    if (token.Length == 0)
    {
      throw new UserTokenUnavailableException("The current request carries an empty bearer token.");
    }

    return token;
  }
}
