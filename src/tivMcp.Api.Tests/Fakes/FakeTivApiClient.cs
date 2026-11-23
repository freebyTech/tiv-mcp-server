using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the API client when a test is about a tool's own job: checking its arguments and
/// choosing the path and query to ask for.
/// </summary>
internal sealed class FakeTivApiClient : ITivApiClient
{
  public string JsonToReturn { get; set; } = "{}";

  /// <summary>Answers for particular paths; any other path gets <see cref="JsonToReturn"/>.</summary>
  public Dictionary<string, string> JsonByPath { get; } = [];

  public List<(string Path, string Body)> Puts { get; } = [];

  public List<(string Path, IReadOnlyDictionary<string, string>? Query)> Calls { get; } = [];

  public Task<string> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
  {
    lock (Calls)
    {
      Calls.Add((path, query));
    }
    return Task.FromResult(JsonByPath.GetValueOrDefault(path, JsonToReturn));
  }

  public Task<string> PutJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    Puts.Add((path, jsonBody));
    return Task.FromResult(jsonBody);
  }
}
