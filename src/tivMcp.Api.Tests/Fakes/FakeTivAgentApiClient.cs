using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the agent API client when a test is about a tool's own job: choosing the route
/// and the body to send, and reading the answer.
/// </summary>
internal sealed class FakeTivAgentApiClient : ITivAgentApiClient
{
  public string JsonToReturn { get; set; } = """{"summary":[]}""";

  public List<(string Path, string Body)> Calls { get; } = [];

  /// <summary>When set, every call fails with this error.</summary>
  public Exception? Failure { get; set; }

  public Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    Calls.Add((path, jsonBody));
    if (Failure != null)
    {
      return Task.FromException<string>(Failure);
    }
    return Task.FromResult(JsonToReturn);
  }
}
