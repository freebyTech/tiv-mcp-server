using tivMcp.Api.Services;

namespace tivMcp.Api.Tests;

/// <summary>
/// The API decides what is allowed; the MCP server's job is to pass its refusals on as a sentence
/// a person can act on, without leaking internals.
/// </summary>
public class TivApiErrorMapperTests
{
  [Fact]
  public void A_401_tells_the_user_to_reconnect()
  {
    var message = TivApiErrorMapper.Describe(401, """{"StatusCode":401,"Message":"Missing required authorization information."}""");

    Assert.Contains("did not accept this sign-in", message);
    Assert.Contains("reconnect", message);
  }

  [Fact]
  public void A_403_passes_on_the_reason_the_api_gave()
  {
    // The body the API writes when an assistant was granted read-only access and tries to write.
    var message = TivApiErrorMapper.Describe(403, """{"StatusCode":403,"Message":"This sign-in was not granted the 'data:write' permission, which this request needs."}""");

    Assert.Equal("trackItsValue refused this request: This sign-in was not granted the 'data:write' permission, which this request needs.", message);
  }

  [Fact]
  public void A_403_for_a_level_rule_passes_on_the_level_message()
  {
    var message = TivApiErrorMapper.Describe(403, """{"statusCode":403,"message":"This action requires user level 3 (you're at 1)."}""");

    Assert.Contains("This action requires user level 3 (you're at 1).", message);
  }

  [Fact]
  public void A_403_with_no_reason_still_explains_itself()
  {
    var message = TivApiErrorMapper.Describe(403, "");

    Assert.Contains("refused this request", message);
    Assert.Contains("does not allow it", message);
  }

  [Fact]
  public void A_402_is_explained_as_a_plan_limit()
  {
    var message = TivApiErrorMapper.Describe(402, """{"StatusCode":402,"Message":"Monthly export limit reached."}""");

    Assert.Equal("This would go over the usage limit of your trackItsValue plan: Monthly export limit reached.", message);
  }

  [Fact]
  public void A_404_and_a_429_have_their_own_wording()
  {
    Assert.Contains("could not find", TivApiErrorMapper.Describe(404, null));
    Assert.Contains("too many requests", TivApiErrorMapper.Describe(429, null));
  }

  [Fact]
  public void A_plain_text_reason_is_passed_on()
  {
    // Controllers answer some conflicts with StatusCode(409, "text").
    var message = TivApiErrorMapper.Describe(409, "The budget is closed.");

    Assert.Equal("trackItsValue could not process this request (status 409): The budget is closed.", message);
  }

  [Fact]
  public void A_json_string_reason_is_unwrapped()
  {
    Assert.EndsWith(": The budget is closed.", TivApiErrorMapper.Describe(409, "\"The budget is closed.\""));
  }

  [Theory]
  [InlineData(500)]
  [InlineData(502)]
  [InlineData(503)]
  public void A_server_fault_never_passes_its_body_on(int statusCode)
  {
    var message = TivApiErrorMapper.Describe(statusCode, """{"StatusCode":500,"Message":"SqlException: Login failed for user 'sa'."}""");

    Assert.DoesNotContain("SqlException", message);
    Assert.Contains($"status {statusCode}", message);
  }

  [Fact]
  public void No_answer_at_all_is_reported_as_unreachable()
  {
    Assert.Contains("could not be reached", TivApiErrorMapper.Describe(0, null));
  }

  [Theory]
  [InlineData("<html><body>Bad Gateway</body></html>")]
  [InlineData("{ not json")]
  [InlineData("[1,2,3]")]
  [InlineData("{\"error\":{\"nested\":true}}")]
  [InlineData("   ")]
  public void Bodies_that_are_not_a_message_are_ignored(string body)
  {
    Assert.Null(TivApiErrorMapper.ExtractMessage(body));
    Assert.Equal("trackItsValue could not process this request (status 400).", TivApiErrorMapper.Describe(400, body));
  }

  [Fact]
  public void The_agent_apis_detail_is_read_as_the_reason()
  {
    Assert.Equal("Unknown tool 'x'.", TivApiErrorMapper.ExtractMessage("""{"detail":"Unknown tool 'x'."}"""));
  }

  [Fact]
  public void A_detail_that_is_a_list_of_validation_errors_is_not_passed_on()
  {
    // The agent API answers a malformed request with a list of field errors, which is not a sentence for a person.
    Assert.Null(TivApiErrorMapper.ExtractMessage("""{"detail":[{"loc":["body","budget"],"msg":"Field required"}]}"""));
  }

  [Fact]
  public void An_overlong_reason_is_not_passed_on()
  {
    Assert.Null(TivApiErrorMapper.ExtractMessage(new string('x', 2000)));
  }
}
