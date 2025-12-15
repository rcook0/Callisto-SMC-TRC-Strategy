namespace TRC.Domain;

/// <summary>
/// OHLCV bar.
/// Use UTC timestamps for consistency across backtest/live.
/// </summary>
public readonly record struct Bar(
  DateTime TimeUtc,
  decimal Open,
  decimal High,
  decimal Low,
  decimal Close,
  decimal Volume
);
