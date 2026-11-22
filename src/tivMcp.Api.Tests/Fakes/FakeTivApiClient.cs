using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tests.Fakes;

/// <summary>
/// Stands in for the API client when a test is about a tool's own job: checking its arguments and
/// choosing the path and query to ask for.
/// </summary>
internal sealed class FakeTivApiClient : ITivApiClient
{
  public string JsonToReturn { get; set; } = "{}";

  public byte[] BytesToReturn { get; set; } = [];

  public List<(string Path, IReadOnlyDictionary<string, string>? Query)> Calls { get; } = [];

  public Task<string> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
  {
    Calls.Add((path, query));
    return Task.FromResult(JsonToReturn);
  }

  public Task<byte[]> GetBytesAsync(string path, CancellationToken cancellationToken = default)
  {
    Calls.Add((path, null));
    return Task.FromResult(BytesToReturn);
  }
}
