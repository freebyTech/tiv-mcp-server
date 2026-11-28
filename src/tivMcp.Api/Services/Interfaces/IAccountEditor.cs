using System.Text.Json;

namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// Runs one account tool end to end as the signed-in user: gets the month's accounts from the
/// trackItsValue API, has the trackItsValue agent API apply the tool to them, and saves the result
/// back through the API the same way the website's account page does. The rules for editing
/// accounts live in the agent API and the rules for who may save live in the API; this only makes
/// the calls in order. The account-side twin of <see cref="IBudgetEditor"/>.
/// </summary>
public interface IAccountEditor
{
  /// <summary>
  /// Applies a tool that changes the month's accounts and saves the result.
  /// </summary>
  /// <param name="year">The year of the accounts month.</param>
  /// <param name="month">The month, 1 to 12.</param>
  /// <param name="tool">The agent API account tool to run, for example "add_account_transaction".</param>
  /// <param name="arguments">The tool's arguments by name. Entries with a null value are left out.</param>
  /// <param name="cancellationToken">Cancels the calls.</param>
  /// <returns>What was changed, one change per line.</returns>
  /// <exception cref="ModelContextProtocol.McpException">An argument is wrong, the tool could not do it, or a service refused.</exception>
  Task<string> ApplyAndSaveAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);

  /// <summary>
  /// Runs a tool that only works something out from the month's accounts, and saves nothing.
  /// </summary>
  /// <param name="year">The year of the accounts month.</param>
  /// <param name="month">The month, 1 to 12.</param>
  /// <param name="tool">The agent API account tool to run, for example "find_account".</param>
  /// <param name="arguments">The tool's arguments by name. Entries with a null value are left out.</param>
  /// <param name="cancellationToken">Cancels the calls.</param>
  /// <returns>What the tool returned.</returns>
  /// <exception cref="ModelContextProtocol.McpException">An argument is wrong, the tool could not do it, or a service refused.</exception>
  Task<JsonElement> QueryAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);
}
