using System.Diagnostics;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using RestSharp;
using tivMcp.Api.Authorization;
using tivMcp.Api.Options;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Services;

/// <summary>
/// Calls the trackItsValue agent API as the signed-in user. It adds the user's token, sends the
/// request and hands back the body untouched; it holds no knowledge of budgets or accounts.
/// </summary>
public class TivAgentApiClient : ITivAgentApiClient
{
  /// <summary>
  /// The name of the HttpClient registration used for calls to the trackItsValue agent API.
  /// </summary>
  public const string HttpClientName = "tivAgentApi";

  private readonly RestClient _restClient;
  private readonly IUserTokenSource _userTokenSource;
  private readonly ICallReference _callReference;
  private readonly ILogger<TivAgentApiClient> _logger;

  /// <summary>
  /// Constructor.
  /// </summary>
  public TivAgentApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<McpAppOptions> mcpAppOptions,
    IUserTokenSource userTokenSource,
    ICallReference callReference,
    ILogger<TivAgentApiClient> logger
  )
  {
    _userTokenSource = userTokenSource;
    _callReference = callReference;
    _logger = logger;
    _restClient = new RestClient(httpClientFactory.CreateClient(HttpClientName), new RestClientOptions(mcpAppOptions.Value.TivAgentApiBaseUrl));
  }

  /// <inheritdoc />
  public async Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    string token;
    try
    {
      token = _userTokenSource.GetApiAccessToken();
    }
    catch (UserTokenUnavailableException)
    {
      throw new McpException(TivApiErrorMapper.Describe(401, null));
    }

    var request = new RestRequest(path);
    request.AddHeader("Authorization", $"Bearer {token}");
    request.AddHeader("Accept", "application/json");
    request.AddHeader(TivCallFailures.ReferenceHeader, _callReference.Value);
    request.AddStringBody(jsonBody, ContentType.Json);

    var started = Stopwatch.GetTimestamp();
    var response = await _restClient.ExecutePostAsync(request, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    if (!response.IsSuccessful)
    {
      throw TivCallFailures.Describe(_logger, TivCallFailures.AgentApi, _callReference.Value, Method.Post, path, response, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    return response.Content ?? string.Empty;
  }
}
