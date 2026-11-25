namespace tivMcp.Api.Models;

/// <summary>
/// One agent API tool to run, with its arguments.
/// </summary>
/// <param name="Tool">The tool's name, for example "add_actual".</param>
/// <param name="Arguments">The tool's arguments under the agent API's own names. Entries with a null value are left out of the request.</param>
public record ToolCall(string Tool, IReadOnlyDictionary<string, object?> Arguments);
