using freebyTech.Common.ExtensionMethods;
using freebyTech.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using Serilog;
using tivMcp.Api.Authorization;
using tivMcp.Api.Constants;
using tivMcp.Api.Options;
using tivMcp.Api.Services;
using tivMcp.Api.Services.Interfaces;
using tivMcp.Api.Tools;

namespace tivMcp.Api;

/// <summary>
/// Startup class for tivMcp.Api application.
/// </summary>
public class Startup
{
  readonly string _appName;
  readonly string _fullVersion;
  readonly IExecutionEnvironment _executionEnvironment;

  /// <summary>
  /// Startup class constructor
  /// </summary>
  public Startup(IExecutionEnvironment executionEnvironment, IConfiguration configuration)
  {
    if (executionEnvironment == null || configuration == null)
    {
      throw new Exception("Startup construction not passed correct paramters.");
    }
    _appName = executionEnvironment.GetAppName();
    _fullVersion = executionEnvironment.GetFullVersion();
    _executionEnvironment = executionEnvironment;
  }

  /// <summary>
  /// This method gets called by the runtime. Use this method to add services to the container.
  /// </summary>
  /// <param name="services"></param>
  /// <param name="configuration"></param>
  public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
  {
    if (services == null)
    {
      throw new Exception("Services collection was not passed to ConfigureServices.");
    }

    if (configuration == null)
    {
      throw new Exception("Configuration collection was not passed to ConfigureServices.");
    }

    Log.Information($"Configuring Services for {_appName} {_fullVersion}");

    // Build out the MCPAPP section for configuration. Every value comes from the environment;
    // there are deliberately no defaults, so a missing setting stops the host instead of silently
    // pointing at some other environment.
    var mcpAppSection = configuration.GetSection(McpAppOptions.SectionName);
    services.Configure<McpAppOptions>(mcpAppSection);

    var mcpAppOptions = mcpAppSection.Get<McpAppOptions>() ?? new McpAppOptions();
    var configurationErrors = McpAppOptionsValidator.Validate(mcpAppOptions);
    if (configurationErrors.Count > 0)
    {
      throw new Exception($"Invalid {McpAppOptions.SectionName} configuration: {string.Join(" ", configurationErrors)}");
    }

    Log.Information($"MCP tools will call the trackItsValue API at '{mcpAppOptions.TivApiBaseUrl}'");
    Log.Information($"MCP server will be validating against '{mcpAppOptions.StsAuthority}' for audience '{mcpAppOptions.StsAudience}'");

    // The server is an OAuth protected resource. Incoming bearer tokens are validated as JWTs
    // (issuer + signing keys discovered from the authority, audience = this server's own URL).
    // An unauthenticated call is challenged by the MCP handler, whose WWW-Authenticate header
    // points the assistant at the protected resource metadata so it can find the authorization
    // server and sign the user in.
    services
      .AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(options =>
      {
        options.Authority = mcpAppOptions.StsAuthority;
        options.Audience = mcpAppOptions.StsAudience;
      })
      .AddMcp(options =>
      {
        options.ResourceMetadata = new()
        {
          Resource = mcpAppOptions.StsAudience,
          AuthorizationServers = { mcpAppOptions.StsAuthority },
          ScopesSupported = [.. McpScopes.All],
        };
      });

    services.AddAuthorization();

    // The server runs behind a TLS-terminating ingress, so requests reach it as plain http. Honor
    // the ingress's X-Forwarded-Proto so the URLs the server writes about itself (the metadata
    // location in the 401 challenge) say https. Only the scheme is taken from the header, never
    // the host or client address. The proxy's address is not known in advance inside a cluster,
    // so the default loopback-only restriction is lifted; the pod is reachable only through the
    // ingress.
    services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
      options.KnownIPNetworks.Clear();
      options.KnownProxies.Clear();
    });

    // The one place that decides which token is presented to the trackItsValue API.
    services.AddHttpContextAccessor();
    services.AddScoped<IUserTokenSource, ForwardedUserTokenSource>();

    // The one client every tool uses to reach the trackItsValue API, as the signed-in user.
    services.AddHttpClient(TivApiClient.HttpClientName);
    services.AddScoped<ITivApiClient, TivApiClient>();

    services.AddSingleton(new ServerInfo(_appName, _fullVersion));

    services
      .AddMcpServer()
      .WithHttpTransport(options =>
      {
        // Stateless: no server-to-client requests are needed, and it lets the server scale
        // horizontally without session affinity.
        options.SessionMode = HttpServerSessionMode.Stateless;
      })
      .WithTools<ServerInfoTools>()
      .WithTools<BudgetTools>()
      .WithTools<AccountTools>()
      .WithTools<UserTools>()
      .WithTools<ExportTools>();
  }

  /// <summary>
  /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
  /// </summary>
  /// <param name="app"></param>
  public void ConfigureApplication(WebApplication app)
  {
    // First, so everything after it sees the scheme the caller actually used.
    app.UseForwardedHeaders();

    if (_executionEnvironment.IsDevelopment() || _executionEnvironment.IsLocal())
    {
      app.UseDeveloperExceptionPage();
    }
    else
    {
      app.UseHsts();
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // Unauthenticated liveness/readiness probe. Returns nothing about users or configuration.
    app.MapGet(McpConstants.HealthPath, () => Results.Ok(new { status = "ok" }));

    // Every MCP request must carry a valid token for this server.
    app.MapMcp(McpConstants.McpPath).RequireAuthorization();
  }
}
