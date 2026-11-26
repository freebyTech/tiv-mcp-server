using ModelContextProtocol;
using tivMcp.Api.Models;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The tool call allowance: the API is asked whether another call is allowed and told when one
/// has been made. The limits are the API's; a problem with the count never fails a tool call.
/// </summary>
public class ToolCallMeterTests
{
  private const string UsedUp =
    """{"allowed":false,"state":"Blocked","bucket":"mcp-tool","tier":"free","enforcement":"Enforce","resetAtUtc":"2026-12-01T00:00:00Z","dimensions":[{"name":"calls","used":2000,"limit":2000,"percent":100,"state":"Critical"}]}""";

  private readonly FakeTivApiClient _api = new();
  private readonly RecordingLogger<ToolCallMeter> _log = new();

  private ToolCallMeter Meter => new(_api, _log);

  [Fact]
  public async Task Asks_the_api_about_the_tool_call_allowance()
  {
    await Meter.RefusalAsync();

    var call = Assert.Single(_api.Calls);
    Assert.Equal("usage/check", call.Path);
    Assert.Equal("mcp-tool", Assert.Single(call.Query!).Value);
    Assert.Equal("bucket", Assert.Single(call.Query!).Key);
  }

  [Theory]
  [InlineData("""{"allowed":true,"tier":"free","dimensions":[{"name":"calls","used":1999,"limit":2000}]}""")]
  [InlineData("""{"allowed":true,"tier":"admin","dimensions":[]}""")]
  [InlineData("{}")]
  [InlineData("")]
  [InlineData("not json")]
  public async Task A_call_goes_ahead_unless_the_api_says_no(string answer)
  {
    _api.JsonToReturn = answer;

    Assert.Null(await Meter.RefusalAsync());
  }

  [Fact]
  public async Task When_the_allowance_is_used_up_the_reason_gives_the_limit_the_plan_and_the_reset_date()
  {
    _api.JsonToReturn = UsedUp;

    Assert.Equal(
      "This month's assistant tool calls are used up: 2,000 are included in the free plan. The count starts again on December 1, 2026 (UTC). "
        + "Nothing was done, and retrying before then will not help. A paid plan includes more.",
      await Meter.RefusalAsync()
    );
  }

  [Fact]
  public void A_paid_plan_is_not_told_to_upgrade()
  {
    var check = new UsageCheck
    {
      Allowed = false,
      Tier = "paid",
      ResetAtUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
      Dimensions =
      [
        new UsageDimension
        {
          Name = "tokens",
          Used = 5,
          Limit = 5,
        },
        new UsageDimension
        {
          Name = "calls",
          Used = 10000,
          Limit = 10000,
        },
      ],
    };

    Assert.Equal(
      "This month's assistant tool calls are used up: 10,000 are included in the paid plan. The count starts again on January 1, 2027 (UTC). Nothing was done, and retrying before then will not help.",
      ToolCallMeter.DescribeRefusal(check)
    );
  }

  [Fact]
  public void A_refusal_without_numbers_still_reads_as_a_sentence()
  {
    Assert.Equal(
      "This month's assistant tool calls included in the user's plan are used up. The count starts again next month. Nothing was done, and retrying before then will not help.",
      ToolCallMeter.DescribeRefusal(new UsageCheck { Allowed = false })
    );
  }

  [Fact]
  public async Task When_the_check_itself_fails_the_call_goes_ahead_and_the_failure_is_logged()
  {
    _api.GetFailures["usage/check"] = new McpException("The trackItsValue API could not be reached.");

    Assert.Null(await Meter.RefusalAsync());
    Assert.Contains("could not be checked", Assert.Single(_log.Entries).Message);
  }

  [Fact]
  public async Task Reports_one_call_to_the_api()
  {
    await Meter.RecordAsync();

    var post = Assert.Single(_api.Posts);
    Assert.Equal("usage/record", post.Path);
    Assert.Equal("""{"bucket":"mcp-tool","calls":1}""", post.Body);
  }

  [Fact]
  public async Task A_report_that_fails_is_logged_and_does_not_fail_the_tool_call()
  {
    _api.PostFailure = new McpException("The trackItsValue API is busy.");

    await Meter.RecordAsync();

    Assert.Contains("could not be counted", Assert.Single(_log.Entries).Message);
  }

  [Fact]
  public void Every_tool_is_counted_except_the_connection_check()
  {
    Assert.False(ToolCallMetering.IsMetered("get_server_info"));
    Assert.True(ToolCallMetering.IsMetered("get_budget_summary"));
    Assert.True(ToolCallMetering.IsMetered("verify_account_transactions"));
    Assert.True(ToolCallMetering.IsMetered("a_tool_added_later"));
  }
}
