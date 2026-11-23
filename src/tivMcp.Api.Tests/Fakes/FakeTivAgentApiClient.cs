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

  public Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    Calls.Add((path, jsonBody));
    return Task.FromResult(JsonToReturn);
  }
}
