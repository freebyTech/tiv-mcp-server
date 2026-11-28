namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// The only way tools reach the trackItsValue API. Every call is made as the signed-in user (see
/// <see cref="Authorization.IUserTokenSource"/>), and every failure is turned into a
/// <see cref="ModelContextProtocol.McpException"/> whose message is safe and clear enough to show
/// the user (see <see cref="TivApiErrorMapper"/>).
/// </summary>
public interface ITivApiClient
{
  /// <summary>
  /// Sends a GET and returns the response body exactly as the API wrote it.
  /// </summary>
  /// <param name="path">The path below the API base URL, for example "budgeting/budgets/list/12". Segments built from user input must already be escaped.</param>
  /// <param name="query">Optional query string parameters.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The API refused the call or could not be reached.</exception>
  Task<string> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Sends a PUT with a JSON body and returns the response body exactly as the API wrote it.
  /// </summary>
  /// <param name="path">The path below the API base URL, for example "budgeting/budgets".</param>
  /// <param name="jsonBody">The request body, already JSON.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The API refused the call or could not be reached.</exception>
  Task<string> PutJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default);

  /// <summary>
  /// Sends a POST with a JSON body and returns the response body exactly as the API wrote it.
  /// </summary>
  /// <param name="path">The path below the API base URL, for example "budgeting/budgets/copy".</param>
  /// <param name="jsonBody">The request body, already JSON.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The API refused the call or could not be reached.</exception>
  Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default);

  /// <summary>
  /// Sends a DELETE with a JSON body and returns the response body exactly as the API wrote it.
  /// </summary>
  /// <param name="path">The path below the API base URL, for example "budgeting/budgets".</param>
  /// <param name="jsonBody">The request body, already JSON.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The API refused the call or could not be reached.</exception>
  Task<string> DeleteJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default);
}
