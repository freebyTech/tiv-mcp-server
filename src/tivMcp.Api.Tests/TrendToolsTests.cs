using ModelContextProtocol;
using tivMcp.Api.Tests.Fakes;
using tivMcp.Api.Tools;

namespace tivMcp.Api.Tests;

public class TrendToolsTests
{
  private const string Groceries = "11111111-1111-1111-1111-111111111111";
  private const string Restaurants = "22222222-2222-2222-2222-222222222222";

  private readonly FakeTivApiClient _api = new();

  private TrendTools Tools => new(_api);

  [Fact]
  public async Task ListTrendItems_returns_the_apis_answer_unchanged()
  {
    _api.JsonToReturn = """[{"continuanceId":"11111111-1111-1111-1111-111111111111", "description":"Groceries","aliases":[]}]""";

    var result = await Tools.ListTrendItems();

    var call = Assert.Single(_api.Calls);
    Assert.Equal("budgeting/lineage/groups", call.Path);
    Assert.Null(call.Query);
    Assert.Equal(_api.JsonToReturn, result);
  }

  [Fact]
  public async Task GetItemTrends_asks_for_the_given_items_and_returns_the_apis_answer_unchanged()
  {
    _api.JsonToReturn = """[{"continuanceId":"11111111-1111-1111-1111-111111111111","year":2026,"month":11, "amountActual":412.55}]""";

    var result = await Tools.GetItemTrends([Groceries, Restaurants]);

    var call = Assert.Single(_api.Calls);
    Assert.Equal("budgeting/lineage/trends", call.Path);
    Assert.Equal("continuanceIds", Assert.Single(call.Query!).Key);
    Assert.Equal($"{Groceries},{Restaurants}", call.Query!["continuanceIds"]);
    Assert.Equal(_api.JsonToReturn, result);
  }

  [Fact]
  public async Task GetItemTrends_sends_each_id_once_in_the_plain_form()
  {
    await Tools.GetItemTrends([$" {Groceries.ToUpperInvariant()} ", "{" + Groceries + "}", Restaurants]);

    Assert.Equal($"{Groceries},{Restaurants}", Assert.Single(_api.Calls).Query!["continuanceIds"]);
  }

  [Theory]
  [InlineData("Groceries")]
  [InlineData("")]
  [InlineData(null)]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  [InlineData("11111111-1111-1111-1111-111111111111,22222222-2222-2222-2222-222222222222")]
  public async Task GetItemTrends_rejects_anything_that_is_not_an_id_instead_of_letting_the_api_drop_it(string? notAnId)
  {
    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetItemTrends([Groceries, notAnId!]));

    Assert.Contains("is not an id", error.Message);
    Assert.Contains("list_trend_items", error.Message);
    Assert.Empty(_api.Calls);
  }

  [Fact]
  public async Task GetItemTrends_needs_at_least_one_id()
  {
    Assert.StartsWith("itemIds must list at least one id", (await Assert.ThrowsAsync<McpException>(() => Tools.GetItemTrends([]))).Message);
    Assert.StartsWith("itemIds must list at least one id", (await Assert.ThrowsAsync<McpException>(() => Tools.GetItemTrends(null!))).Message);
    Assert.Empty(_api.Calls);
  }

  [Fact]
  public async Task GetItemTrends_takes_fifty_ids_and_no_more()
  {
    await Tools.GetItemTrends(Enumerable.Range(0, 50).Select(_ => Guid.NewGuid().ToString()).ToArray());
    Assert.Equal(50, Assert.Single(_api.Calls).Query!["continuanceIds"].Split(',').Length);

    var error = await Assert.ThrowsAsync<McpException>(() => Tools.GetItemTrends(Enumerable.Range(0, 51).Select(_ => Guid.NewGuid().ToString()).ToArray()));

    Assert.StartsWith("itemIds can list at most 50 ids, but 51 were given.", error.Message);
    Assert.Single(_api.Calls);
  }
}
