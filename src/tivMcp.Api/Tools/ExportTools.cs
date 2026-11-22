using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Read-only tool over the trackItsValue API's data export.
/// </summary>
[McpServerToolType]
public sealed class ExportTools
{
  /// <summary>
  /// The entry inside the export archive that holds the user's data.
  /// </summary>
  public const string DataEntryName = "data.json";

  private readonly ITivApiClient _tivApi;

  /// <summary>
  /// Constructor.
  /// </summary>
  public ExportTools(ITivApiClient tivApi)
  {
    _tivApi = tivApi;
  }

  /// <summary>
  /// GET export. The API answers with a zip archive; its data.json is returned as it is.
  /// </summary>
  [McpServerTool(Name = "export_data", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "Export a complete copy of the signed-in user's own trackItsValue data as JSON: one flat array per record type (accounts, account categories, account actuals, budgets, budget categories, items, actuals and more), all ids as GUIDs. "
      + "This is the user's entire history, so the result can be very large; prefer get_budget or get_accounts_overview for a question about one month. "
      + "Exports count against the user's plan: on the free plan only one export a month is allowed, so ask the user before calling this."
  )]
  public async Task<string> ExportData(CancellationToken cancellationToken = default)
  {
    var archiveBytes = await _tivApi.GetBytesAsync("export", cancellationToken);

    try
    {
      using var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
      var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, DataEntryName, StringComparison.OrdinalIgnoreCase));
      if (entry == null)
      {
        throw new McpException($"The trackItsValue export did not contain {DataEntryName}.");
      }

      using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
      return await reader.ReadToEndAsync(cancellationToken);
    }
    catch (InvalidDataException)
    {
      throw new McpException("trackItsValue returned an export that could not be opened.");
    }
  }
}
