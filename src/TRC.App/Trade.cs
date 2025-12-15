namespace TRC.App;

/// <summary>
/// Minimal trade model (expand later: fills, partials, R-multiples, fees, etc.).
/// </summary>
public sealed record Trade(
  long TradeId,
  DateTime TimeUtc,
  string Symbol,
  string Side,          // "BUY" or "SELL"
  decimal EntryPrice,
  decimal? StopLoss,
  decimal? TakeProfit,
  long SetupSeq
);
