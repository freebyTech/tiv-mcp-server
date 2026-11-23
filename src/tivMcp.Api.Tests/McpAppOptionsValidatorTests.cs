using tivMcp.Api.Options;

namespace tivMcp.Api.Tests;

/// <summary>
/// The server ships with no configuration defaults (nothing may silently point at production), so
/// startup depends on this validator naming every setting that is missing or malformed.
/// </summary>
public class McpAppOptionsValidatorTests
{
  private static McpAppOptions ValidOptions() =>
    new()
    {
      TivApiBaseUrl = "http://localhost:5000/tiv",
      TivAgentApiBaseUrl = "http://localhost:8000",
      StsAuthority = "https://example.auth0.com/",
      StsAudience = "https://mcp.example.test/mcp",
    };

  [Fact]
  public void Audience_must_be_the_server_url()
  {
    // The audience is also published as the OAuth resource identifier, which must be a URL.
    var options = ValidOptions();
    options.StsAudience = "example-audience";

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains("MCPAPP__STSAUDIENCE must be an absolute http or https URL", errors[0]);
  }

  [Fact]
  public void Fully_configured_options_are_valid()
  {
    Assert.Empty(McpAppOptionsValidator.Validate(ValidOptions()));
  }

  [Fact]
  public void Missing_section_is_reported()
  {
    var errors = McpAppOptionsValidator.Validate(null);

    Assert.Single(errors);
    Assert.Contains("MCPAPP", errors[0]);
  }

  [Fact]
  public void Unconfigured_options_report_every_setting()
  {
    var errors = McpAppOptionsValidator.Validate(new McpAppOptions());

    Assert.Equal(4, errors.Count);
    Assert.Contains(errors, e => e.Contains("MCPAPP__TIVAPIBASEURL"));
    Assert.Contains(errors, e => e.Contains("MCPAPP__TIVAGENTAPIBASEURL"));
    Assert.Contains(errors, e => e.Contains("MCPAPP__STSAUTHORITY"));
    Assert.Contains(errors, e => e.Contains("MCPAPP__STSAUDIENCE"));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(null)]
  public void Blank_api_base_url_is_rejected(string? value)
  {
    var options = ValidOptions();
    options.TivApiBaseUrl = value;

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains("MCPAPP__TIVAPIBASEURL is not set", errors[0]);
  }

  [Theory]
  [InlineData("localhost:5000/tiv")]
  [InlineData("/tiv")]
  [InlineData("ftp://example.com/tiv")]
  public void Api_base_url_must_be_an_absolute_http_url(string value)
  {
    var options = ValidOptions();
    options.TivApiBaseUrl = value;

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains("MCPAPP__TIVAPIBASEURL must be an absolute http or https URL", errors[0]);
  }

  [Theory]
  [InlineData(null, "MCPAPP__TIVAGENTAPIBASEURL is not set")]
  [InlineData("agent:8000", "MCPAPP__TIVAGENTAPIBASEURL must be an absolute http or https URL")]
  public void Agent_api_base_url_must_be_set_to_an_absolute_http_url(string? value, string expected)
  {
    var options = ValidOptions();
    options.TivAgentApiBaseUrl = value;

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains(expected, errors[0]);
  }

  [Fact]
  public void Authority_must_be_an_absolute_http_url()
  {
    var options = ValidOptions();
    options.StsAuthority = "example.auth0.com";

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains("MCPAPP__STSAUTHORITY", errors[0]);
  }

  [Fact]
  public void Blank_audience_is_rejected()
  {
    var options = ValidOptions();
    options.StsAudience = " ";

    var errors = McpAppOptionsValidator.Validate(options);

    Assert.Single(errors);
    Assert.Contains("MCPAPP__STSAUDIENCE", errors[0]);
  }
}
