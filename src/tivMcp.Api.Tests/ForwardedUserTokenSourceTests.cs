using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using tivMcp.Api.Authorization;

namespace tivMcp.Api.Tests;

/// <summary>
/// ForwardedUserTokenSource is the only code that decides which token reaches the trackItsValue
/// API. It must hand over exactly the caller's validated token and refuse in every other case.
/// </summary>
public class ForwardedUserTokenSourceTests
{
  private static ForwardedUserTokenSource SourceFor(HttpContext? httpContext) => new(new HttpContextAccessor { HttpContext = httpContext });

  private static DefaultHttpContext Request(string? authorizationHeader, bool authenticated = true)
  {
    var httpContext = new DefaultHttpContext();
    if (authorizationHeader != null)
    {
      httpContext.Request.Headers.Authorization = authorizationHeader;
    }
    if (authenticated)
    {
      httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "auth0|test-user")], authenticationType: "Bearer"));
    }
    return httpContext;
  }

  [Theory]
  [InlineData("Bearer abc.def.ghi")]
  [InlineData("bearer abc.def.ghi")]
  [InlineData("Bearer   abc.def.ghi  ")]
  public void Returns_the_callers_token_without_the_scheme(string authorizationHeader)
  {
    var token = SourceFor(Request(authorizationHeader)).GetApiAccessToken();

    Assert.Equal("abc.def.ghi", token);
  }

  [Fact]
  public void Refuses_when_there_is_no_current_request()
  {
    Assert.Throws<UserTokenUnavailableException>(() => SourceFor(null).GetApiAccessToken());
  }

  [Fact]
  public void Refuses_a_token_that_was_not_validated()
  {
    // A bearer header on a request the JWT handler did not authenticate must never be forwarded.
    var source = SourceFor(Request("Bearer abc.def.ghi", authenticated: false));

    Assert.Throws<UserTokenUnavailableException>(() => source.GetApiAccessToken());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Basic dXNlcjpwYXNz")]
  [InlineData("Bearer ")]
  public void Refuses_when_no_bearer_token_is_present(string? authorizationHeader)
  {
    var source = SourceFor(Request(authorizationHeader));

    Assert.Throws<UserTokenUnavailableException>(() => source.GetApiAccessToken());
  }
}
