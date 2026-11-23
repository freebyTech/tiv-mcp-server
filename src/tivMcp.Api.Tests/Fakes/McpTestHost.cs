using System.Security.Cryptography;
using freebyTech.Common.Environment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using tivMcp.Api.Services;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Runs the real <see cref="Startup"/> pipeline on an in-memory test server. The only substitution
/// is the authorization server: instead of fetching signing keys from the authority over the
/// network, the JWT handler is handed a fixed issuer and a locally generated key, so tests can mint
/// tokens that the unmodified validation rules accept or reject.
/// </summary>
internal sealed class McpTestHost : IAsyncDisposable
{
  public const string Authority = "https://auth.example.test/";
  public const string Audience = "https://mcp.example.test/mcp";
  public const string TivApiBaseUrl = "https://api.example.test/tiv";
  public const string TivAgentApiBaseUrl = "https://agent.example.test/agent";

  private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-key" };

  private readonly WebApplication _app;

  public HttpClient Client { get; }

  private McpTestHost(WebApplication app)
  {
    _app = app;
    Client = app.GetTestClient();
  }

  /// <param name="tivApi">Stands in for the trackItsValue API; when omitted, any call to the API fails the test.</param>
  /// <param name="tivAgentApi">Stands in for the trackItsValue agent API; when omitted, any call to it fails the test.</param>
  public static async Task<McpTestHost> StartAsync(HttpMessageHandler? tivApi = null, HttpMessageHandler? tivAgentApi = null)
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(
      new Dictionary<string, string?>
      {
        ["MCPAPP:TIVAPIBASEURL"] = TivApiBaseUrl,
        ["MCPAPP:TIVAGENTAPIBASEURL"] = TivAgentApiBaseUrl,
        ["MCPAPP:STSAUTHORITY"] = Authority,
        ["MCPAPP:STSAUDIENCE"] = Audience,
      }
    );

    var executionEnvironment = new ExecutionEnvironment(new EnvironmentManager(), typeof(Startup).Assembly);
    var startup = new Startup(executionEnvironment, builder.Configuration);
    startup.ConfigureServices(builder.Services, builder.Configuration);

    var openIdConfiguration = new OpenIdConnectConfiguration { Issuer = Authority };
    openIdConfiguration.SigningKeys.Add(SigningKey);
    builder.Services.PostConfigure<JwtBearerOptions>(
      JwtBearerDefaults.AuthenticationScheme,
      options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(openIdConfiguration)
    );

    // No test ever reaches a real API: outbound calls go to the stand-in.
    var tivApiHandler = tivApi ?? new FakeTivApi(_ => throw new InvalidOperationException("This test did not expect a call to the trackItsValue API."));
    builder.Services.AddHttpClient(TivApiClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => tivApiHandler);
    var tivAgentApiHandler = tivAgentApi ?? new FakeTivApi(_ => throw new InvalidOperationException("This test did not expect a call to the trackItsValue agent API."));
    builder.Services.AddHttpClient(TivAgentApiClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => tivAgentApiHandler);

    var app = builder.Build();
    startup.ConfigureApplication(app);
    await app.StartAsync();
    return new McpTestHost(app);
  }

  /// <summary>
  /// Mints a signed access token. Defaults describe a token the server must accept; each argument
  /// lets a test break exactly one property.
  /// </summary>
  public static string CreateToken(string issuer = Authority, string audience = Audience, DateTime? expires = null, SecurityKey? signingKey = null)
  {
    var expiresOn = expires ?? DateTime.UtcNow.AddMinutes(10);
    var descriptor = new SecurityTokenDescriptor
    {
      Issuer = issuer,
      Audience = audience,
      NotBefore = expiresOn.AddHours(-1),
      IssuedAt = expiresOn.AddHours(-1),
      Expires = expiresOn,
      Claims = new Dictionary<string, object> { ["sub"] = "auth0|test-user" },
      SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
    };
    return new JsonWebTokenHandler().CreateToken(descriptor);
  }

  public async ValueTask DisposeAsync()
  {
    Client.Dispose();
    await _app.DisposeAsync();
  }
}
