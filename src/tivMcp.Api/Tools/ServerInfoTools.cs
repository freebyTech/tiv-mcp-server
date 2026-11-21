using System.ComponentModel;
using ModelContextProtocol.Server;

namespace tivMcp.Api.Tools;

/// <summary>
/// The name and version of this MCP server, registered once at startup.
/// </summary>
public record ServerInfo(string Name, string Version);

/// <summary>
/// Tools that describe the MCP server itself. They touch no user data and call no API.
/// </summary>
[McpServerToolType]
public sealed class ServerInfoTools
{
  private readonly ServerInfo _serverInfo;

  /// <summary>
  /// Constructor.
  /// </summary>
  public ServerInfoTools(ServerInfo serverInfo)
  {
    _serverInfo = serverInfo;
  }

  /// <summary>
  /// Returns the name and version of the running MCP server.
  /// </summary>
  [McpServerTool(Name = "get_server_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description("Get the name and version of the trackItsValue MCP server. Useful to confirm the connection works.")]
  public ServerInfo GetServerInfo()
  {
    return _serverInfo;
  }
}
