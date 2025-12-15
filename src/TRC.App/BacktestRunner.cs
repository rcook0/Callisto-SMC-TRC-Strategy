using TRC.Domain;

namespace TRC.App;

public sealed class BacktestRunner
{
  private readonly IBarSource _bars;
  private readonly IHtfBiasService _bias;
  private readonly IEventSink _eventSink;
  private readonly ITradeSink _tradeSink;
  private readonly IExecutionModel _exec;

  public BacktestRunner(
    IBarSource bars,
    IHtfBiasService bias,
    IEventSink eventSink,
    ITradeSink tradeSink,
    IExecutionModel exec)
  {
    _bars = bars;
    _bias = bias;
    _eventSink = eventSink;
    _tradeSink = tradeSink;
    _exec = exec;
  }

  public async Task<BacktestResult> RunAsync(BacktestRequest req, CancellationToken ct)
  {
    var engine = new TrcEngine(req.TrcConfig);

    long barsProcessed = 0;
    long eventsWritten = 0;
    long tradesWritten = 0;
    long tradeId = 0;

    var started = DateTime.UtcNow;

    await foreach (var bar in _bars.ReadAllAsync(ct))
    {
      if (req.StartUtc is not null && bar.TimeUtc < req.StartUtc.Value) continue;
      if (req.EndUtc   is not null && bar.TimeUtc > req.EndUtc.Value)   break;

      barsProcessed++;

      var htfBias = _bias.OnBaseBar(bar);
      var events = engine.OnBar(bar, htfBias);

      foreach (var e in events)
      {
        await _eventSink.WriteAsync(e, ct);
        eventsWritten++;
      }

      foreach (var trade in _exec.OnEvents(bar, events, htfBias))
      {
        var t = trade with { TradeId = ++tradeId, Symbol = req.Symbol };
        await _tradeSink.WriteAsync(t, ct);
        tradesWritten++;
      }
    }

    await _eventSink.FlushAsync(ct);
    await _tradeSink.FlushAsync(ct);

    var finished = DateTime.UtcNow;

    return new BacktestResult(
      Symbol: req.Symbol,
      BarsProcessed: barsProcessed,
      EventsWritten: eventsWritten,
      TradesWritten: tradesWritten,
      StartedUtc: started,
      FinishedUtc: finished
    );
  }
}
