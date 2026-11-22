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
    var response = await ExecuteGetAsync(path, query, "application/json", cancellationToken);
    return response.Content ?? string.Empty;
  }

  /// <inheritdoc />
  public async Task<byte[]> GetBytesAsync(string path, CancellationToken cancellationToken = default)
  {
    var response = await ExecuteGetAsync(path, null, "*/*", cancellationToken);
    return response.RawBytes ?? [];
  }

  private async Task<RestResponse> ExecuteGetAsync(string path, IReadOnlyDictionary<string, string>? query, string accept, CancellationToken cancellationToken)
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
    request.AddHeader("Accept", accept);
    if (query != null)
    {
      foreach (var parameter in query)
      {
        request.AddQueryParameter(parameter.Key, parameter.Value);
      }
    }

    var response = await _restClient.ExecuteGetAsync(request, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    if (!response.IsSuccessful)
    {
      throw new McpException(TivApiErrorMapper.Describe((int)response.StatusCode, response.Content));
    }

    return response;
  }
}
