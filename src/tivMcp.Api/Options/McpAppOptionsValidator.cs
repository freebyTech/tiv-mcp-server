namespace tivMcp.Api.Options;

/// <summary>
/// Pure validation of <see cref="McpAppOptions"/>. The server has no built-in defaults, so every
/// setting must be supplied by the environment; this reports each one that is missing or malformed.
/// </summary>
public static class McpAppOptionsValidator
{
  /// <summary>
  /// Validates the options and returns one message per problem. An empty list means valid.
  /// </summary>
  public static IReadOnlyList<string> Validate(McpAppOptions options)
  {
    var errors = new List<string>();

    if (options == null)
    {
      errors.Add($"The {McpAppOptions.SectionName} configuration section is missing.");
      return errors;
    }

    ValidateAbsoluteHttpUrl(errors, options.TivApiBaseUrl, "TIVAPIBASEURL");
    ValidateAbsoluteHttpUrl(errors, options.StsAuthority, "STSAUTHORITY");
    // The audience doubles as the OAuth resource identifier, which the MCP authorization spec
    // requires to be the server's URL.
    ValidateAbsoluteHttpUrl(errors, options.StsAudience, "STSAUDIENCE");

    return errors;
  }

  private static void ValidateAbsoluteHttpUrl(List<string> errors, string value, string settingName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      errors.Add($"{McpAppOptions.SectionName}__{settingName} is not set.");
      return;
    }

    var isHttpUrl = Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    if (!isHttpUrl)
    {
      errors.Add($"{McpAppOptions.SectionName}__{settingName} must be an absolute http or https URL.");
    }
  }
}
