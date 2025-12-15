using TRC.App;
using TRC.Domain;

namespace TRC.Infra.Execution;

/// <summary>
/// Paper execution model:
/// - Converts TRC entry events into Trade records.
/// - Does NOT model fills/slippage/partials yet (that lives here long-term).
/// </summary>
public sealed class PaperExecutionModel : IExecutionModel
{
  public IEnumerable<Trade> OnEvents(in Bar bar, IReadOnlyList<TrcEvent> events, int htfBias)
  {
    foreach (var e in events)
    {
      if (e.Type == TrcEventType.TrcLongEntry)
      {
        yield return CreateTrade(bar, side: "BUY", e);
      }
      else if (e.Type == TrcEventType.TrcShortEntry)
      {
        yield return CreateTrade(bar, side: "SELL", e);
      }
    }
  }

  private static Trade CreateTrade(in Bar bar, string side, TrcEvent e)
  {
    var setupSeq = e.Info.TryGetValue("setupSeq", out var ss) && ss is long l ? l : 0L;

    decimal? zoneLow = e.Info.TryGetValue("entryZoneLow", out var zl) && zl is decimal d1 ? d1 : null;
    decimal? zoneHigh = e.Info.TryGetValue("entryZoneHigh", out var zh) && zh is decimal d2 ? d2 : null;

    // Placeholder: SL at far side of zone; TP null.
    decimal? sl = side == "BUY" ? zoneLow : zoneHigh;

    return new Trade(
      TradeId: 0, // assigned by runner
      TimeUtc: bar.TimeUtc,
      Symbol: "", // assigned by runner
      Side: side,
      EntryPrice: bar.Close,
      StopLoss: sl,
      TakeProfit: null,
      SetupSeq: setupSeq
    );
  }
}
