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
  /// Sends a GET and returns the raw response bytes, for endpoints that answer with a file.
  /// </summary>
  /// <param name="path">The path below the API base URL.</param>
  /// <param name="cancellationToken">Cancels the call.</param>
  /// <exception cref="ModelContextProtocol.McpException">The API refused the call or could not be reached.</exception>
  Task<byte[]> GetBytesAsync(string path, CancellationToken cancellationToken = default);
}
