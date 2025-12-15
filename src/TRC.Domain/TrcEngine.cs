using System.Collections.Generic;

namespace TRC.Domain;

/// <summary>
/// Canonical TRC engine:
/// - Maintains incremental market structure (BOS/CHoCH) on the execution timeframe (e.g. 5m).
/// - Emits events for BOS, CHoCH, TRC setup, and TRC entry (on retest).
/// - HTF bias is provided externally each bar (e.g. from 4H/1H/15m module).
///
/// This is intentionally "pure domain": no UI, no broker, no storage.
/// </summary>
public sealed class TrcEngine
{
  private readonly TrcConfig _cfg;
  private readonly TrcState _s;

  private long _barIndex = 0;

  public TrcState State => _s;

  public TrcEngine(TrcConfig? cfg = null, TrcState? state = null)
  {
    _cfg = cfg ?? new TrcConfig();
    _s = state ?? new TrcState();
  }

  /// <summary>
  /// Process one bar.
  /// htfBias: +1 bullish, -1 bearish, 0 neutral.
  /// </summary>
  public IReadOnlyList<TrcEvent> OnBar(in Bar bar, int htfBias)
  {
    _barIndex++;

    var events = new List<TrcEvent>(capacity: 4);

    if (_barIndex == 1)
    {
      Bootstrap(bar);
      return events;
    }

    // 1) Update structure (BOS/CHoCH)
    var structEvt = UpdateStructure(bar);
    if (structEvt is not null)
    {
      events.Add(structEvt);

      // 2) Create TRC setup on CHoCH that aligns with HTF bias.
      if (structEvt.Type == TrcEventType.ChochUp && htfBias == 1)
      {
        CreateSetup(bar, direction: 1);
        events.Add(new TrcEvent(bar.TimeUtc, TrcEventType.TrcLongSetup,
          new Dictionary<string, object?>
          {
            ["setupSeq"] = _s.SetupSeq,
            ["zoneLow"] = _s.ZoneLow,
            ["zoneHigh"] = _s.ZoneHigh
          }));
      }
      else if (structEvt.Type == TrcEventType.ChochDown && htfBias == -1)
      {
        CreateSetup(bar, direction: -1);
        events.Add(new TrcEvent(bar.TimeUtc, TrcEventType.TrcShortSetup,
          new Dictionary<string, object?>
          {
            ["setupSeq"] = _s.SetupSeq,
            ["zoneLow"] = _s.ZoneLow,
            ["zoneHigh"] = _s.ZoneHigh
          }));
      }
    }

    // 3) Check retest (entry)
    var entryEvt = CheckRetest(bar);
    if (entryEvt is not null)
      events.Add(entryEvt);

    return events;
  }

  private void Bootstrap(in Bar bar)
  {
    _s.LastHigh = bar.High;
    _s.LastLow  = bar.Low;
    _s.PendingLow = bar.Low;
    _s.PendingHigh = bar.High;

    _s.Trend = bar.Close >= bar.Open ? Trend.Bull : Trend.Bear;
  }

  private TrcEvent? UpdateStructure(in Bar bar)
  {
    // If trend undefined, infer from bar direction.
    if (_s.Trend == Trend.None)
      _s.Trend = bar.Close >= bar.Open ? Trend.Bull : Trend.Bear;

    return _s.Trend switch
    {
      Trend.Bull => UpdateBull(bar),
      Trend.Bear => UpdateBear(bar),
      _ => null
    };
  }

  /// <summary>
  /// Bull structure:
  /// - BOS up: new impulse high; confirms lastHL as the pullback low since prior impulse high.
  /// - CHoCH down: close below lastHL.
  /// </summary>
  private TrcEvent? UpdateBull(in Bar bar)
  {
    // CHoCH down: close below last confirmed HL.
    if (_s.LastHL is not null)
    {
      var chochDown = _cfg.RequireCloseBeyondStructure
        ? bar.Close < _s.LastHL.Value
        : bar.Low   < _s.LastHL.Value;

      if (chochDown)
      {
        // flip trend
        _s.Trend = Trend.Bear;

        // seed bear tracking
        _s.LastHigh = bar.High;
        _s.LastLow  = bar.Low;
        _s.PendingHigh = bar.High;
        _s.PendingLow = null;

        // for bear CHoCH we can treat the current high as initial LH seed
        _s.LastLH = bar.High;

        return new TrcEvent(bar.TimeUtc, TrcEventType.ChochDown,
          new Dictionary<string, object?>
          {
            ["level"] = _s.LastHL,
            ["barClose"] = bar.Close
          });
      }
    }

    // Update pullback candidate low
    _s.PendingLow = _s.PendingLow is null ? bar.Low : Math.Min(_s.PendingLow.Value, bar.Low);

    // BOS up: break to new high
    var isBosUp = _s.LastHigh is null || bar.High > _s.LastHigh.Value;
    if (isBosUp)
    {
      _s.LastHigh = bar.High;

      // Confirm HL from pullback candidate (if we have one)
      if (_s.PendingLow is not null)
        _s.LastHL = _s.PendingLow.Value;

      // reset pullback candidate after impulse
      _s.PendingLow = bar.Low;

      return new TrcEvent(bar.TimeUtc, TrcEventType.BosUp,
        new Dictionary<string, object?>
        {
          ["level"] = _s.LastHigh,
          ["lastHL"] = _s.LastHL
        });
    }

    return null;
  }

  /// <summary>
  /// Bear structure:
  /// - BOS down: new impulse low; confirms lastLH as the pullback high since prior impulse low.
  /// - CHoCH up: close above lastLH.
  /// </summary>
  private TrcEvent? UpdateBear(in Bar bar)
  {
    // CHoCH up: close above last confirmed LH.
    if (_s.LastLH is not null)
    {
      var chochUp = _cfg.RequireCloseBeyondStructure
        ? bar.Close > _s.LastLH.Value
        : bar.High  > _s.LastLH.Value;

      if (chochUp)
      {
        _s.Trend = Trend.Bull;

        _s.LastHigh = bar.High;
        _s.LastLow  = bar.Low;
        _s.PendingLow = bar.Low;
        _s.PendingHigh = null;

        // seed bull: current low as initial HL
        _s.LastHL = bar.Low;

        return new TrcEvent(bar.TimeUtc, TrcEventType.ChochUp,
          new Dictionary<string, object?>
          {
            ["level"] = _s.LastLH,
            ["barClose"] = bar.Close
          });
      }
    }

    // Update pullback candidate high
    _s.PendingHigh = _s.PendingHigh is null ? bar.High : Math.Max(_s.PendingHigh.Value, bar.High);

    // BOS down: break to new low
    var isBosDown = _s.LastLow is null || bar.Low < _s.LastLow.Value;
    if (isBosDown)
    {
      _s.LastLow = bar.Low;

      // Confirm LH from pullback candidate (if we have one)
      if (_s.PendingHigh is not null)
        _s.LastLH = _s.PendingHigh.Value;

      // reset pullback candidate after impulse
      _s.PendingHigh = bar.High;

      return new TrcEvent(bar.TimeUtc, TrcEventType.BosDown,
        new Dictionary<string, object?>
        {
          ["level"] = _s.LastLow,
          ["lastLH"] = _s.LastLH
        });
    }

    return null;
  }

  private void CreateSetup(in Bar bar, int direction)
  {
    _s.SetupSeq++;
    _s.SetupDir = direction;
    _s.BarsLeft = _cfg.MaxRetestBars;
    _s.SetupStartedBarIndex = _barIndex;

    // Define a simple retest zone from the CHoCH bar.
    // Long: [low, close]
    // Short: [close, high]
    if (direction == 1)
    {
      var lo = Math.Min(bar.Low, bar.Close);
      var hi = Math.Max(bar.Low, bar.Close);
      _s.ZoneLow = lo;
      _s.ZoneHigh = hi;
    }
    else
    {
      var lo = Math.Min(bar.Close, bar.High);
      var hi = Math.Max(bar.Close, bar.High);
      _s.ZoneLow = lo;
      _s.ZoneHigh = hi;
    }
  }

  private TrcEvent? CheckRetest(in Bar bar)
  {
    if (_s.SetupDir == 0 || _s.BarsLeft <= 0 || _s.ZoneLow is null || _s.ZoneHigh is null)
      return null;

    // Decrement first so "MaxRetestBars=1" means "next bar only".
    _s.BarsLeft--;

    // Do not allow entry on the setup bar itself.
    if (_barIndex <= _s.SetupStartedBarIndex)
      return null;

    if (_s.SetupDir == 1)
    {
      // Long retest: bar trades down into zone.
      var hit = bar.Low <= _s.ZoneHigh.Value && bar.Low >= _s.ZoneLow.Value;
      if (hit)
      {
        var evt = new TrcEvent(bar.TimeUtc, TrcEventType.TrcLongEntry,
          new Dictionary<string, object?>
          {
            ["setupSeq"] = _s.SetupSeq,
            ["entryZoneLow"] = _s.ZoneLow,
            ["entryZoneHigh"] = _s.ZoneHigh
          });

        _s.ClearSetup();
        return evt;
      }
    }
    else if (_s.SetupDir == -1)
    {
      // Short retest: bar trades up into zone.
      var hit = bar.High >= _s.ZoneLow.Value && bar.High <= _s.ZoneHigh.Value;
      if (hit)
      {
        var evt = new TrcEvent(bar.TimeUtc, TrcEventType.TrcShortEntry,
          new Dictionary<string, object?>
          {
            ["setupSeq"] = _s.SetupSeq,
            ["entryZoneLow"] = _s.ZoneLow,
            ["entryZoneHigh"] = _s.ZoneHigh
          });

        _s.ClearSetup();
        return evt;
      }
    }

    // Expire setup if time runs out
    if (_s.BarsLeft <= 0)
      _s.ClearSetup();

    return null;
  }
}
