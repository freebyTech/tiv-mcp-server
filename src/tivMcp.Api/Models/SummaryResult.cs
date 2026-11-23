using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The agent API's answer to a summarize request: the summary as lines of text.
/// </summary>
public class SummaryResult
{
  /// <summary>
  /// The summary, one line per element.
  /// </summary>
  [JsonPropertyName("summary")]
  public List<string> Summary { get; set; }
}
