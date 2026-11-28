using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Services;

/// <summary>
/// Puts the tool call allowance in front of every tool: the API is asked before a tool runs, and
/// told after one succeeds. A call that is refused, or that fails, is not counted.
/// </summary>
public static class ToolCallMetering
{
  // Reads nothing of the user's and calls no service; it is how a connection is confirmed.
  private static readonly HashSet<string> Unmetered = new(StringComparer.Ordinal) { "get_server_info" };

  /// <summary>
  /// Whether calls to the named tool count against the allowance.
  /// </summary>
  public static bool IsMetered(string? toolName) => toolName == null || !Unmetered.Contains(toolName);

  /// <summary>
  /// The filter to register with the MCP server's call-tool pipeline.
  /// </summary>
  public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
  {
    return async (context, cancellationToken) =>
    {
      var meter = IsMetered(context.Params?.Name) ? context.Services?.GetService<IToolCallMeter>() : null;
      if (meter == null)
      {
        return await next(context, cancellationToken);
      }

      var refusal = await meter.RefusalAsync(cancellationToken);
      if (refusal != null)
      {
        return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = refusal }] };
      }

      var result = await next(context, cancellationToken);
      if (result.IsError != true)
      {
        await meter.RecordAsync(cancellationToken);
      }
      return result;
    };
  }
}
