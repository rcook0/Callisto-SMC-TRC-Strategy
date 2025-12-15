using TRC.App;
using TRC.Domain;

namespace TRC.Infra.MarketData;

/// <summary>
/// HTF bias implementation using:
/// - period aggregation to 15m, 1h, 4h
/// - SMA on the HTF closes
/// - bias per TF: close > sma => +1, close < sma => -1, else 0
/// - final bias: 2-of-3 vote
///
/// This is an intentionally simple placeholder.
/// Swap later with structure-based HTF bias without changing TRC.Domain.
/// </summary>
public sealed class HtfBiasServiceSma : IHtfBiasService
{
  private readonly BarAggregator _agg15;
  private readonly BarAggregator _agg1h;
  private readonly BarAggregator _agg4h;

  private readonly RollingSma _sma15;
  private readonly RollingSma _sma1h;
  private readonly RollingSma _sma4h;

  private int _b15 = 0;
  private int _b1h = 0;
  private int _b4h = 0;

  public HtfBiasServiceSma(int smaLen = 50)
  {
    _agg15 = new BarAggregator(Timeframe.M15.ToTimeSpan());
    _agg1h = new BarAggregator(Timeframe.H1.ToTimeSpan());
    _agg4h = new BarAggregator(Timeframe.H4.ToTimeSpan());

    _sma15 = new RollingSma(smaLen);
    _sma1h = new RollingSma(smaLen);
    _sma4h = new RollingSma(smaLen);
  }

  public int OnBaseBar(in Bar baseBar)
  {
    UpdateOne(_agg15, _sma15, ref _b15, baseBar);
    UpdateOne(_agg1h, _sma1h, ref _b1h, baseBar);
    UpdateOne(_agg4h, _sma4h, ref _b4h, baseBar);

    var sum = _b15 + _b1h + _b4h;
    return sum >= 2 ? 1 : sum <= -2 ? -1 : 0;
  }

  private static void UpdateOne(BarAggregator agg, RollingSma sma, ref int bias, in Bar baseBar)
  {
    var htf = agg.OnBaseBar(baseBar);
    if (htf is null) return;

    var m = sma.Push(htf.Value.Close);
    if (m is null)
    {
      bias = 0;
      return;
    }

    bias = htf.Value.Close > m.Value ? 1 : htf.Value.Close < m.Value ? -1 : 0;
  }
}
