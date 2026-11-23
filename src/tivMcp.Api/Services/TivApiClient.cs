using Microsoft.Extensions.Options;
using ModelContextProtocol;
using RestSharp;
using tivMcp.Api.Authorization;
using tivMcp.Api.Options;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Services;

/// <summary>
/// Calls the trackItsValue API as the signed-in user. It adds the user's token, sends the request
/// and hands back the body untouched; it holds no knowledge of budgets or accounts.
/// </summary>
public class TivApiClient : ITivApiClient
{
  /// <summary>
  /// The name of the HttpClient registration used for calls to the trackItsValue API.
  /// </summary>
  public const string HttpClientName = "tivApi";

  private readonly RestClient _restClient;
  private readonly IUserTokenSource _userTokenSource;

  /// <summary>
  /// Constructor.
  /// </summary>
  public TivApiClient(IHttpClientFactory httpClientFactory, IOptions<McpAppOptions> mcpAppOptions, IUserTokenSource userTokenSource)
  {
    _userTokenSource = userTokenSource;
    _restClient = new RestClient(httpClientFactory.CreateClient(HttpClientName), new RestClientOptions(mcpAppOptions.Value.TivApiBaseUrl));
  }

  /// <inheritdoc />
  public async Task<string> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
  {
    var request = new RestRequest(path);
    if (query != null)
    {
      foreach (var parameter in query)
      {
        request.AddQueryParameter(parameter.Key, parameter.Value);
      }
    }

    return await ExecuteAsync(request, Method.Get, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<string> PutJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    var request = new RestRequest(path);
    request.AddStringBody(jsonBody, ContentType.Json);

    return await ExecuteAsync(request, Method.Put, cancellationToken);
  }

  private async Task<string> ExecuteAsync(RestRequest request, Method method, CancellationToken cancellationToken)
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

    request.AddHeader("Authorization", $"Bearer {token}");
    request.AddHeader("Accept", "application/json");

    var response = await _restClient.ExecuteAsync(request, method, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    if (!response.IsSuccessful)
    {
      throw new McpException(TivApiErrorMapper.Describe((int)response.StatusCode, response.Content));
    }

    return response.Content ?? string.Empty;
  }
}
