using TRC.App;

namespace TRC.Infra.Data;

public sealed class CsvTradeSink : ITradeSink, IAsyncDisposable
{
  private readonly StreamWriter _sw;
  private bool _headerWritten;

  public CsvTradeSink(string path)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    _sw = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));
  }

  public async ValueTask WriteAsync(Trade trade, CancellationToken ct)
  {
    if (!_headerWritten)
    {
      await _sw.WriteLineAsync("tradeId,timeUtc,symbol,side,entry,sl,tp,setupSeq").ConfigureAwait(false);
      _headerWritten = true;
    }

    var line =
      $"{trade.TradeId},{trade.TimeUtc:O},{trade.Symbol},{trade.Side}," +
      $"{trade.EntryPrice},{trade.StopLoss},{trade.TakeProfit},{trade.SetupSeq}";
    await _sw.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
  }

  public async ValueTask FlushAsync(CancellationToken ct)
  {
    await _sw.FlushAsync().ConfigureAwait(false);
  }

  public async ValueTask DisposeAsync()
  {
    await _sw.DisposeAsync().ConfigureAwait(false);
  }
}
