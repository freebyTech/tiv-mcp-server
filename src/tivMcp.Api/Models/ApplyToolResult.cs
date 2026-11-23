using System.Text.Json;
using System.Text.Json.Serialization;

namespace tivMcp.Api.Models;

/// <summary>
/// The agent API's answer to an apply-tool request.
/// </summary>
public class ApplyToolResult
{
  /// <summary>
  /// Whether the tool did what was asked.
  /// </summary>
  [JsonPropertyName("success")]
  public bool Success { get; set; }

  /// <summary>
  /// The tool's own message when it did not succeed.
  /// </summary>
  [JsonPropertyName("error")]
  public string Error { get; set; }

  /// <summary>
  /// What the tool changed.
  /// </summary>
  [JsonPropertyName("changes")]
  public List<ToolChange> Changes { get; set; }

  /// <summary>
  /// The edited budget, ready to save. It is kept as the agent API wrote it and never read here.
  /// </summary>
  [JsonPropertyName("budget")]
  public JsonElement Budget { get; set; }

  /// <summary>
  /// Anything else the tool returned, for example a list of accounts.
  /// </summary>
  [JsonPropertyName("data")]
  public JsonElement Data { get; set; }
}

/// <summary>
/// One change a tool made.
/// </summary>
public class ToolChange
{
  /// <summary>
  /// The change in words.
  /// </summary>
  [JsonPropertyName("description")]
  public string Description { get; set; }
}
