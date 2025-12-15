using TRC.Domain;

namespace TRC.App;

/// <summary>
/// Market data source for base timeframe bars (e.g. 5m).
/// </summary>
public interface IBarSource
{
  IAsyncEnumerable<Bar> ReadAllAsync(CancellationToken ct);
}

/// <summary>
/// Supplies HTF bias (+1 bull, -1 bear, 0 neutral) for each base bar timestamp.
/// Implementations may aggregate bars internally.
/// </summary>
public interface IHtfBiasService
{
  /// <summary>Update internal HTF state with the new base bar, and return latest bias.</summary>
  int OnBaseBar(in Bar baseBar);
}

/// <summary>
/// Sinks events for audit/replay.
/// </summary>
public interface IEventSink
{
  ValueTask WriteAsync(TrcEvent evt, CancellationToken ct);
  ValueTask FlushAsync(CancellationToken ct);
}

/// <summary>
/// Sinks trades.
/// </summary>
public interface ITradeSink
{
  ValueTask WriteAsync(Trade trade, CancellationToken ct);
  ValueTask FlushAsync(CancellationToken ct);
}

/// <summary>
/// Converts strategy events into trades (paper/live execution logic).
/// </summary>
public interface IExecutionModel
{
  IEnumerable<Trade> OnEvents(in Bar bar, IReadOnlyList<TrcEvent> events, int htfBias);
}
