using System.Text.Json;
using TRC.App;

namespace TRC.Infra.Data;

public static class SummaryWriter
{
  public static async Task WriteAsync(string path, BacktestResult result, CancellationToken ct)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
  }
}
