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

  /// <summary>
  /// Constructor.
  /// </summary>
  public TivAgentApiClient(IHttpClientFactory httpClientFactory, IOptions<McpAppOptions> mcpAppOptions, IUserTokenSource userTokenSource)
  {
    _userTokenSource = userTokenSource;
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
    request.AddStringBody(jsonBody, ContentType.Json);

    var response = await _restClient.ExecutePostAsync(request, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    if (!response.IsSuccessful)
    {
      throw new McpException(TivApiErrorMapper.Describe((int)response.StatusCode, response.Content));
    }

    return response.Content ?? string.Empty;
  }
}
