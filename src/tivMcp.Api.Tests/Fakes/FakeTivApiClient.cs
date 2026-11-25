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

  public List<(string Path, string Body)> Posts { get; } = [];

  public List<(string Path, IReadOnlyDictionary<string, string>? Query)> Calls { get; } = [];

  /// <summary>Paths whose GET fails with the given error instead of answering.</summary>
  public Dictionary<string, Exception> GetFailures { get; } = [];

  /// <summary>When set, every PUT fails with this error.</summary>
  public Exception? PutFailure { get; set; }

  public Task<string> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
  {
    lock (Calls)
    {
      Calls.Add((path, query));
    }
    if (GetFailures.TryGetValue(path, out var failure))
    {
      return Task.FromException<string>(failure);
    }
    return Task.FromResult(JsonByPath.GetValueOrDefault(path, JsonToReturn));
  }

  public Task<string> PutJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    Puts.Add((path, jsonBody));
    if (PutFailure != null)
    {
      return Task.FromException<string>(PutFailure);
    }
    return Task.FromResult(jsonBody);
  }

  public Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken cancellationToken = default)
  {
    Posts.Add((path, jsonBody));
    return Task.FromResult(JsonByPath.GetValueOrDefault(path, JsonToReturn));
  }
}
