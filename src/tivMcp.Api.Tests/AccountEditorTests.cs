using System.Text.Json;
using ModelContextProtocol;
using tivMcp.Api.Services;
using tivMcp.Api.Tests.Fakes;

namespace tivMcp.Api.Tests;

/// <summary>
/// The account editor makes the calls for one account change in order: get the month's accounts,
/// the account templates and the account types from the API, have the agent API apply the tool,
/// and save the edited accounts through the API. It must pass everything along unchanged and never
/// save after a failure.
/// </summary>
public class AccountEditorTests
{
  private const string AccountsPath = "accounting/accounts/all/2026/11";
  private const string TemplatesPath = "accounting/templates";
  private const string TypesPath = "accounting/accounts/allAccountTypes";

  // Odd spacing and number formatting, to show the JSON is passed along as written.
  private const string AccountsJson = """{"relevantYear":2026, "relevantMonth":11,"accountTypes":[{"delta":12.50}]}""";
  private const string TemplatesJson = """[{"id":"t-1","description":"Checking Account"}]""";
  private const string TypesJson = """[{"id":1,"descriptionSingular":"Bank Account"}]""";
  private const string EditedJson = """{"relevantYear":2026, "accountTypes":[{"isChildDirty":true,"delta":12.50}]}""";

  private static readonly Dictionary<string, object?> Rename = new()
  {
    ["account"] = "Main Checking",
    ["new_description"] = "Joint Checking",
    ["category"] = null,
  };

  private readonly FakeTivApiClient _api = new();
  private readonly FakeTivAgentApiClient _agentApi = new();

  public AccountEditorTests()
  {
    _api.JsonByPath[AccountsPath] = AccountsJson;
    _api.JsonByPath[TemplatesPath] = TemplatesJson;
    _api.JsonByPath[TypesPath] = TypesJson;
    _agentApi.JsonToReturn =
      """{"success":true,"error":null,"changes":[{"description":"Renamed account 'Main Checking' to 'Joint Checking'"}],"accounts":""" + EditedJson + ""","data":{"account":{"id":"a-1"}}}""";
  }

  private AccountEditor Editor => new(_api, _agentApi);

  private JsonElement SentToAgent => JsonDocument.Parse(Assert.Single(_agentApi.Calls).Body).RootElement;

  [Fact]
  public async Task Gets_the_months_accounts_the_templates_and_the_account_types()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename);

    Assert.Equal([AccountsPath, TypesPath, TemplatesPath], _api.Calls.Select(call => call.Path).Order());
  }

  [Fact]
  public async Task Sends_the_agent_api_the_tool_its_arguments_and_what_the_api_returned_unchanged()
  {
    await Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename);

    Assert.Equal("budget-agent/apply-account-tools", _agentApi.Calls[0].Path);
    var call = Assert.Single(SentToAgent.GetProperty("calls").EnumerateArray());
    Assert.Equal("rename_account", call.GetProperty("tool").GetString());
    Assert.Equal("Joint Checking", call.GetProperty("arguments").GetProperty("new_description").GetString());
    // An argument the caller left out is not sent at all.
    Assert.False(call.GetProperty("arguments").TryGetProperty("category", out _));
    var context = SentToAgent.GetProperty("context");
    Assert.Equal(AccountsJson, context.GetProperty("accounts").GetRawText());
    Assert.Equal(TemplatesJson, context.GetProperty("account-templates").GetRawText());
    Assert.Equal(TypesJson, context.GetProperty("account-types").GetRawText());
  }

  [Fact]
  public async Task Saves_the_edited_accounts_exactly_as_the_agent_api_returned_them_and_reports_the_changes()
  {
    var answer = await Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename);

    var put = Assert.Single(_api.Puts);
    Assert.Equal("accounting/accounts", put.Path);
    Assert.Equal(EditedJson, put.Body);
    Assert.Equal("Saved. Renamed account 'Main Checking' to 'Joint Checking'", answer);
  }

  [Fact]
  public async Task A_query_returns_what_the_tool_found_and_saves_nothing()
  {
    var data = await Editor.QueryAsync(2026, 11, "find_account", Rename);

    Assert.Equal("a-1", data.GetProperty("account").GetProperty("id").GetString());
    Assert.Empty(_api.Puts);
  }

  [Fact]
  public async Task When_the_tool_cannot_do_it_nothing_is_saved_and_its_reason_is_passed_on()
  {
    _agentApi.JsonToReturn = """{"success":false,"error":"Account 'Nowhere' not found. There are: 'Main Checking'.","changes":[],"accounts":null,"data":{}}""";

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename));

    Assert.Equal("Account 'Nowhere' not found. There are: 'Main Checking'. Nothing was saved.", error.Message);
    Assert.Empty(_api.Puts);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not json")]
  [InlineData("""{"success":true,"changes":[],"accounts":null}""")]
  public async Task An_answer_that_cannot_be_read_saves_nothing(string answer)
  {
    _agentApi.JsonToReturn = answer;

    var error = await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename));

    Assert.EndsWith("Nothing was saved.", error.Message);
    Assert.Empty(_api.Puts);
  }

  [Fact]
  public async Task A_failure_before_the_save_names_the_step_and_says_nothing_was_saved()
  {
    _api.GetFailures[TemplatesPath] = new TivCallFailedException("The service did not answer.", 0, "trackItsValue", "ref12345");
    var reading = await Assert.ThrowsAsync<TivCallFailedException>(() => Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename));
    _api.GetFailures.Clear();
    _agentApi.Failure = new TivCallFailedException("The request was refused.", 403, "the agent API", "ref12345");
    var working = await Assert.ThrowsAsync<TivCallFailedException>(() => Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename));

    Assert.Equal("Reading the account templates failed. The service did not answer. Nothing was saved.", reading.Message);
    Assert.Equal("Working out the change failed. The request was refused. Nothing was saved.", working.Message);
    Assert.Empty(_api.Puts);
  }

  [Theory]
  [InlineData(409, "Saving the accounts failed. Closed months are read-only. Nothing was saved.")]
  [InlineData(500, "Saving the accounts failed. Closed months are read-only. The save may or may not have gone through. Read the accounts again before retrying, so nothing is added twice.")]
  public async Task A_failed_save_says_whether_anything_may_have_been_written(int status, string expected)
  {
    _api.PutFailure = new TivCallFailedException("Closed months are read-only.", status, "trackItsValue", "ref12345");

    var error = await Assert.ThrowsAsync<TivCallFailedException>(() => Editor.ApplyAndSaveAsync(2026, 11, "rename_account", Rename));

    Assert.Equal(expected, error.Message);
  }

  [Fact]
  public async Task A_bad_month_stops_before_any_call()
  {
    await Assert.ThrowsAsync<McpException>(() => Editor.ApplyAndSaveAsync(2026, 13, "rename_account", Rename));

    Assert.Empty(_api.Calls);
    Assert.Empty(_agentApi.Calls);
  }
}
