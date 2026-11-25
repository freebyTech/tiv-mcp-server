using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

/// <summary>
/// verify_account_transactions checks the shape of its arguments, posts them to the API's
/// verify-transactions endpoint and returns the API's answer as it is. Which account is meant,
/// which transactions are covered and whether it is allowed are the API's decisions.
/// </summary>
public class AccountEditToolsTests
{
  private const string Answer = """{"accountName":"James Discover Card","markedCount":12,"remainingInEarlierMonthsCount":0,"endingBalance":-2804.91}""";

  private readonly FakeTivApiClient _api = new() { JsonToReturn = Answer };

  private AccountEditTools Tools => new(_api);

  private string Body
  {
    get
    {
      var post = Assert.Single(_api.Posts);
      Assert.Equal("accounting/accounts/verify-transactions", post.Path);
      return post.Body;
    }
  }

  [Fact]
  public async Task Verifies_the_whole_month_by_default()
  {
    await Tools.VerifyAccountTransactions("James Discover Card", 2026, 10);

    Assert.Equal("""{"accountName":"James Discover Card","year":2026,"month":10,"verified":true,"dryRun":false}""", Body);
  }

  [Fact]
  public async Task Passes_on_the_through_day_the_unverify_and_the_dry_run()
  {
    await Tools.VerifyAccountTransactions(" James Discover Card ", 2026, 10, throughDay: 25, verified: false, dryRun: true);

    Assert.Equal("""{"accountName":"James Discover Card","year":2026,"month":10,"throughDay":25,"verified":false,"dryRun":true}""", Body);
  }

  [Fact]
  public async Task An_account_given_as_an_id_is_sent_as_an_id()
  {
    await Tools.VerifyAccountTransactions("0F8FAD5B-D9CB-469F-A165-70867728950E", 2026, 10);

    Assert.Equal("""{"accountId":"0f8fad5b-d9cb-469f-a165-70867728950e","year":2026,"month":10,"verified":true,"dryRun":false}""", Body);
  }

  [Fact]
  public async Task Returns_the_apis_answer_unchanged()
  {
    Assert.Equal(Answer, await Tools.VerifyAccountTransactions("James Discover Card", 2026, 10));
  }

  [Fact]
  public async Task Makes_one_call_and_reads_nothing_first()
  {
    await Tools.VerifyAccountTransactions("James Discover Card", 2026, 10);

    Assert.Single(_api.Posts);
    Assert.Empty(_api.Calls);
    Assert.Empty(_api.Puts);
  }

  [Fact]
  public async Task A_through_day_past_the_end_of_the_month_is_left_for_the_api_to_read_as_the_whole_month()
  {
    await Tools.VerifyAccountTransactions("James Discover Card", 2026, 11, throughDay: 31);

    Assert.Contains("\"throughDay\":31", Body);
  }

  [Theory]
  [InlineData(null, 2026, 10, null, "account")]
  [InlineData("   ", 2026, 10, null, "account")]
  [InlineData("Card", 1999, 10, null, "year")]
  [InlineData("Card", 2026, 0, null, "month")]
  [InlineData("Card", 2026, 13, null, "month")]
  [InlineData("Card", 2026, 10, 0, "throughDay")]
  [InlineData("Card", 2026, 10, 32, "throughDay")]
  public async Task A_bad_argument_stops_before_any_call(string? account, int year, int month, int? throughDay, string named)
  {
    var error = await Assert.ThrowsAnyAsync<McpException>(async () => await Tools.VerifyAccountTransactions(account!, year, month, throughDay));

    Assert.StartsWith(named, error.Message);
    Assert.Empty(_api.Posts);
  }
}
