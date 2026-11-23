namespace tivMcp.Api.Services.Interfaces;

/// <summary>
/// Runs one budget tool end to end as the signed-in user: gets the budget from the trackItsValue
/// API, has the trackItsValue agent API apply the tool to it, and saves the result back through
/// the API the same way the website does. The rules for editing a budget live in the agent API and
/// the rules for who may save live in the API; this only makes the calls in order.
/// </summary>
public interface IBudgetEditor
{
  /// <summary>
  /// Applies a tool that changes the budget and saves the result.
  /// </summary>
  /// <param name="year">The budget's year.</param>
  /// <param name="month">The budget's month, 1 to 12.</param>
  /// <param name="budgetName">The budget's name, or null or blank for the main budget.</param>
  /// <param name="tool">The agent API tool to run, for example "add_actual".</param>
  /// <param name="arguments">The tool's arguments by name. Entries with a null value are left out.</param>
  /// <param name="cancellationToken">Cancels the calls.</param>
  /// <returns>What was changed, one change per line.</returns>
  /// <exception cref="ModelContextProtocol.McpException">An argument is wrong, the tool could not do it, or a service refused.</exception>
  Task<string> ApplyAndSaveAsync(int year, int month, string? budgetName, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);

  /// <summary>
  /// Runs a tool that only reads from the budget and its accounts. Nothing is saved.
  /// </summary>
  /// <param name="year">The budget's year.</param>
  /// <param name="month">The budget's month, 1 to 12.</param>
  /// <param name="budgetName">The budget's name, or null or blank for the main budget.</param>
  /// <param name="tool">The agent API tool to run, for example "get_available_accounts".</param>
  /// <param name="cancellationToken">Cancels the calls.</param>
  /// <returns>The tool's data as JSON, exactly as the agent API wrote it.</returns>
  /// <exception cref="ModelContextProtocol.McpException">An argument is wrong, the tool could not do it, or a service refused.</exception>
  Task<string> QueryAsync(int year, int month, string? budgetName, string tool, CancellationToken cancellationToken = default);
}
