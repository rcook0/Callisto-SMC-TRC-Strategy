using TRC.Domain;

namespace TRC.Infra.MarketData;

/// <summary>
/// Aggregates base bars into higher timeframe bars, emitting only on completed periods.
/// </summary>
public sealed class BarAggregator
{
  private readonly TimeSpan _period;

  private DateTime? _currentPeriodStartUtc;
  private decimal _open;
  private decimal _high;
  private decimal _low;
  private decimal _close;
  private decimal _vol;
  private bool _hasBar;

  public BarAggregator(TimeSpan period)
  {
    if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period));
    _period = period;
  }

  /// <summary>
  /// Feed a base bar. Returns a completed aggregated bar if the period rolled.
  /// Otherwise returns null.
  /// </summary>
  public Bar? OnBaseBar(in Bar b)
  {
    var ps = FloorToPeriodStart(b.TimeUtc, _period);

    if (!_hasBar)
    {
      StartNew(ps, b);
      return null;
    }

    if (ps != _currentPeriodStartUtc)
    {
      // close previous period and start new
      var completed = new Bar(
        TimeUtc: _currentPeriodStartUtc!.Value,
        Open: _open,
        High: _high,
        Low: _low,
        Close: _close,
        Volume: _vol);

      StartNew(ps, b);
      return completed;
    }

    // update current
    _high = Math.Max(_high, b.High);
    _low = Math.Min(_low, b.Low);
    _close = b.Close;
    _vol += b.Volume;

    return null;
  }

  private void StartNew(DateTime periodStartUtc, in Bar b)
  {
    _hasBar = true;
    _currentPeriodStartUtc = periodStartUtc;
    _open = b.Open;
    _high = b.High;
    _low = b.Low;
    _close = b.Close;
    _vol = b.Volume;
  }

  private static DateTime FloorToPeriodStart(DateTime utc, TimeSpan period)
  {
    // Use ticks floor (UTC assumed).
    long ticks = utc.Ticks;
    long p = period.Ticks;
    long floored = ticks - (ticks % p);
    return new DateTime(floored, DateTimeKind.Utc);
  }
}
