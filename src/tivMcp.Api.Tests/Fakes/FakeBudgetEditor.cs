using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the budget editor when a test is about a tool's own job: checking its arguments
/// and naming the agent API tool and arguments to run.
/// </summary>
internal sealed class FakeBudgetEditor : IBudgetEditor
{
  public sealed record Call(int Year, int Month, string? BudgetName, string Tool, IReadOnlyDictionary<string, object?> Arguments, bool Saves);

  public List<Call> Calls { get; } = [];

  public Call SingleCall => Assert.Single(Calls);

  public Task<string> ApplyAndSaveAsync(int year, int month, string? budgetName, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    Calls.Add(new Call(year, month, budgetName, tool, arguments, Saves: true));
    return Task.FromResult("Saved.");
  }

  public Task<string> QueryAsync(int year, int month, string? budgetName, string tool, CancellationToken cancellationToken = default)
  {
    Calls.Add(new Call(year, month, budgetName, tool, new Dictionary<string, object?>(), Saves: false));
    return Task.FromResult("{}");
  }
}
