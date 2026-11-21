using System.Reflection;
using ModelContextProtocol.Server;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

public class ServerInfoToolsTests
{
  [Fact]
  public void GetServerInfo_returns_the_registered_name_and_version()
  {
    var tools = new ServerInfoTools(new ServerInfo("tivMcp.Api", "1.2.3.4"));

    var info = tools.GetServerInfo();

    Assert.Equal("tivMcp.Api", info.Name);
    Assert.Equal("1.2.3.4", info.Version);
  }

  [Fact]
  public void GetServerInfo_is_published_as_a_read_only_tool()
  {
    // Assistants decide whether to ask the user before calling a tool from these annotations, so
    // a read tool that lost its read-only marking would start prompting users needlessly.
    var method = typeof(ServerInfoTools).GetMethod(nameof(ServerInfoTools.GetServerInfo));
    var attribute = method.GetCustomAttribute<McpServerToolAttribute>();

    Assert.NotNull(attribute);
    Assert.Equal("get_server_info", attribute.Name);
    Assert.True(attribute.ReadOnly);
    Assert.False(attribute.Destructive);
  }
}
