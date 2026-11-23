namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// The only way tools reach the trackItsValue agent API, which converts the API's full budget and
/// account JSON into the compact form an assistant reads. Every call is made as the signed-in user
/// (see <see cref="Authorization.IUserTokenSource"/>), and every failure is turned into a
/// <see cref="ModelContextProtocol.McpException"/> whose message is safe and clear enough to show
/// the user (see <see cref="TivApiErrorMapper"/>).
/// </summary>
public interface ITivAgentApiClient
{
  /// <summary>
  /// Sends a POST with a JSON body and returns the response body exactly as the agent API wrote it.
  /// </summary>
  /// <param name="path">The path below the agent API base URL, for example "budget-agent/summarize-budget".</param>
  /// <param name="jsonBody">The request body, already JSON.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The agent API refused the call or could not be reached.</exception>
  Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default);
}
