using System.Globalization;
using TRC.App;
using TRC.Domain;

namespace TRC.Infra.MarketData;

/// <summary>
/// Streaming CSV bar reader.
/// Expected header: time,open,high,low,close,volume
/// time should be ISO-8601 (preferably UTC 'Z').
/// </summary>
public sealed class CsvBarSource : IBarSource
{
  private readonly string _path;

  public CsvBarSource(string path)
  {
    _path = path;
  }

  public async IAsyncEnumerable<Bar> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
  {
    using var fs = File.OpenRead(_path);
    using var sr = new StreamReader(fs);

    var header = await sr.ReadLineAsync(ct);
    if (header is null)
      yield break;

    while (!sr.EndOfStream)
    {
      ct.ThrowIfCancellationRequested();
      var line = await sr.ReadLineAsync(ct);
      if (string.IsNullOrWhiteSpace(line))
        continue;

      // naive CSV split; suitable for clean numeric data exports
      var parts = line.Split(',');
      if (parts.Length < 6)
        continue;

      var time = DateTime.Parse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
      var open = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
      var high = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
      var low  = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
      var close= decimal.Parse(parts[4], CultureInfo.InvariantCulture);
      var vol  = decimal.Parse(parts[5], CultureInfo.InvariantCulture);

      yield return new Bar(time, open, high, low, close, vol);
    }
  }
}
