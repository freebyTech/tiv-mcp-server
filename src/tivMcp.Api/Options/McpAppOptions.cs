namespace tivMcp.Api.Options;

/// <summary>
/// Class representing the MCP App Options environment variables used by the MCP server.
/// </summary>
public class McpAppOptions
{
  /// <summary>
  /// The configuration section these options are bound from.
  /// </summary>
  public const string SectionName = "MCPAPP";

  /// <summary>
  /// The base URL of the trackItsValue API that every tool call is passed through to, pulled from
  /// the environment variable MCPAPP__TIVAPIBASEURL.
  /// </summary>
  public string TivApiBaseUrl { get; set; }

  /// <summary>
  /// The Authority for the Security Token Service, pulled from the environment variable
  /// MCPAPP__STSAUTHORITY.
  /// </summary>
  public string StsAuthority { get; set; }

  /// <summary>
  /// The audience incoming tokens must be issued for, pulled from the environment variable
  /// MCPAPP__STSAUDIENCE. This is the MCP server's own public URL (including the /mcp path),
  /// registered with the authorization server as an API identifier. It is also published as the
  /// resource in the OAuth protected resource metadata, so assistants request tokens for it.
  /// </summary>
  public string StsAudience { get; set; }
}
