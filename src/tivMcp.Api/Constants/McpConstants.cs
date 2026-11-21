namespace tivMcp.Api.Constants;

/// <summary>
/// Route constants for the MCP server.
/// </summary>
public class McpConstants
{
  /// <summary>
  /// The Streamable HTTP endpoint that MCP clients connect to.
  /// </summary>
  public const string McpPath = "/mcp";

  /// <summary>
  /// The unauthenticated health probe used by the container orchestrator.
  /// </summary>
  public const string HealthPath = "/health";
}
