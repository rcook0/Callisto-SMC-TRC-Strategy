using TRC.Domain;

namespace TRC.App;

public sealed record BacktestRequest(
  string Symbol,
  TrcConfig TrcConfig,
  DateTime? StartUtc = null,
  DateTime? EndUtc = null
);

public sealed record BacktestResult(
  string Symbol,
  long BarsProcessed,
  long EventsWritten,
  long TradesWritten,
  DateTime StartedUtc,
  DateTime FinishedUtc
);
