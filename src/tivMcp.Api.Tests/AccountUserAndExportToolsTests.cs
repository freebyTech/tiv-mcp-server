using System.IO.Compression;
using System.Text;
using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

public class AccountToolsTests
{
  private readonly FakeTivApiClient _api = new();

  [Fact]
  public async Task ListAccounts_reads_the_plain_account_list()
  {
    _api.JsonToReturn = """[{"description":"Checking"}]""";

    var result = await new AccountTools(_api).ListAccounts();

    Assert.Equal("accounting/accounts/all", Assert.Single(_api.Calls).Path);
    Assert.Equal(_api.JsonToReturn, result);
  }

  [Fact]
  public async Task GetAccountsOverview_asks_for_the_month()
  {
    _api.JsonToReturn = """{"relevantYear":2026,"relevantMonth":11}""";

    var result = await new AccountTools(_api).GetAccountsOverview(2026, 11);

    Assert.Equal("accounting/accounts/all/2026/11", Assert.Single(_api.Calls).Path);
    Assert.Equal(_api.JsonToReturn, result);
  }

  [Theory]
  [InlineData(1999, 6)]
  [InlineData(2026, 0)]
  [InlineData(2026, 13)]
  public async Task GetAccountsOverview_rejects_an_impossible_month_without_calling_the_api(int year, int month)
  {
    await Assert.ThrowsAsync<McpException>(() => new AccountTools(_api).GetAccountsOverview(year, month));

    Assert.Empty(_api.Calls);
  }
}

public class UserToolsTests
{
  private readonly FakeTivApiClient _api = new();

  [Fact]
  public async Task GetUserStatus_reads_the_status_endpoint()
  {
    _api.JsonToReturn = """{"level":4,"experience":1250}""";

    var result = await new UserTools(_api).GetUserStatus();

    Assert.Equal("user/status", Assert.Single(_api.Calls).Path);
    Assert.Equal(_api.JsonToReturn, result);
  }

  [Fact]
  public async Task GetTotalWealthOutlook_defaults_to_all_time()
  {
    await new UserTools(_api).GetTotalWealthOutlook();

    var call = Assert.Single(_api.Calls);
    Assert.Equal("dashboards/total-wealth", call.Path);
    Assert.Equal("all", Assert.Single(call.Query!).Value);
    Assert.Equal("range", Assert.Single(call.Query!).Key);
  }

  [Theory]
  [InlineData("1yr", "1yr")]
  [InlineData(" 5YR ", "5yr")]
  [InlineData("10yr", "10yr")]
  public async Task GetTotalWealthOutlook_sends_the_range_in_the_apis_spelling(string given, string sent)
  {
    await new UserTools(_api).GetTotalWealthOutlook(given);

    Assert.Equal(sent, Assert.Single(_api.Calls).Query!["range"]);
  }

  [Theory]
  [InlineData("forever")]
  [InlineData("")]
  [InlineData("1y")]
  public async Task GetTotalWealthOutlook_rejects_an_unknown_range_instead_of_letting_it_mean_all_time(string range)
  {
    // The API silently treats an unknown range as "all"; an assistant that asked for one year
    // must not be handed every year without knowing.
    var error = await Assert.ThrowsAsync<McpException>(() => new UserTools(_api).GetTotalWealthOutlook(range));

    Assert.Contains("range must be one of", error.Message);
    Assert.Empty(_api.Calls);
  }
}

public class ExportToolsTests
{
  private readonly FakeTivApiClient _api = new();

  private static byte[] Zip(params (string Name, string Content)[] entries)
  {
    using var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
      foreach (var (name, content) in entries)
      {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
      }
    }
    return stream.ToArray();
  }

  [Fact]
  public async Task ExportData_returns_the_data_file_from_the_archive_unchanged()
  {
    // Indentation, key order and non-ASCII text must survive, so the export can later be fed
    // back to an import exactly as the API produced it.
    const string data = "{\r\n  \"accounts\": [ { \"description\": \"Épargne — joint\", \"id\": \"0a1b\" } ],\r\n  \"budgets\": []\r\n}";
    _api.BytesToReturn = Zip(("data.json", data), ("README.txt", "trackItsValue data export"));

    var result = await new ExportTools(_api).ExportData();

    Assert.Equal("export", Assert.Single(_api.Calls).Path);
    Assert.Equal(data, result);
  }

  [Fact]
  public async Task ExportData_reports_an_archive_without_the_data_file()
  {
    _api.BytesToReturn = Zip(("README.txt", "trackItsValue data export"));

    var error = await Assert.ThrowsAsync<McpException>(() => new ExportTools(_api).ExportData());

    Assert.Contains("did not contain data.json", error.Message);
  }

  [Fact]
  public async Task ExportData_reports_a_response_that_is_not_an_archive()
  {
    _api.BytesToReturn = Encoding.UTF8.GetBytes("<html>Bad Gateway</html>");

    var error = await Assert.ThrowsAsync<McpException>(() => new ExportTools(_api).ExportData());

    Assert.Contains("could not be opened", error.Message);
  }
}
