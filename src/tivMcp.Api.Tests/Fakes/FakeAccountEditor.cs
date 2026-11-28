using System.Text.Json;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the account editor when a test is about a tool's own job: checking its arguments
/// and naming the agent API tool and arguments to run.
/// </summary>
internal sealed class FakeAccountEditor : IAccountEditor
{
  public sealed record Call(int Year, int Month, string Tool, IReadOnlyDictionary<string, object?> Arguments, bool Saves);

  public List<Call> Calls { get; } = [];

  public Call SingleCall => Assert.Single(Calls);

  /// <summary>What a query answers with.</summary>
  public string DataToReturn { get; set; } = "{}";

  public Task<string> ApplyAndSaveAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    Calls.Add(new Call(year, month, tool, arguments, Saves: true));
    return Task.FromResult("Saved.");
  }

  public Task<JsonElement> QueryAsync(int year, int month, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
  {
    Calls.Add(new Call(year, month, tool, arguments, Saves: false));
    return Task.FromResult(JsonDocument.Parse(DataToReturn).RootElement.Clone());
  }
}
