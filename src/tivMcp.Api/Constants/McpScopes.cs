namespace tivMcp.Api.Constants;

/// <summary>
/// The OAuth scopes the MCP server advertises. They are consent for the assistant, granted by the
/// user at sign-in; the trackItsValue API remains the place where they are enforced.
/// </summary>
public class McpScopes
{
  /// <summary>
  /// Lets an assistant read the user's data.
  /// </summary>
  public const string DataRead = "data:read";

  /// <summary>
  /// Lets an assistant change the user's data.
  /// </summary>
  public const string DataWrite = "data:write";

  /// <summary>
  /// Every scope, in the order published in the protected resource metadata.
  /// </summary>
  public static readonly string[] All = [DataRead, DataWrite];
}
