using System;
using System.Collections.Generic;

namespace TRC.Core;

/// <summary>
/// Canonical TRC engine:
/// - Maintains incremental 5-minute structure (BOS / CHOCH).
/// - Uses last HL / LH for CHOCH (ICT/SMC-aligned).
/// - Creates retest zones after CHOCH in direction of HTF bias.
/// 
/// HTF bias (+1 bull, -1 bear, 0 neutral) is supplied externally per bar.
/// </summary>
public sealed class TrcEngine
{
    private readonly TrcConfig _config;
    private readonly TrcState  _state;

    private int _barIndex;

    public TrcState State => _state;

    public TrcEngine(TrcConfig? config = null)
    {
        _config = config ?? TrcConfig.Default;
        _state  = new TrcState();
        _barIndex = 0;
    }

    /// <summary>
    /// Feed one bar of data and get any TRC-relevant events emitted on this bar.
    /// htfBias: +1 (bull), -1 (bear), 0 (neutral) from external HTF logic.
    /// </summary>
    public IReadOnlyList<TrcEvent> OnBar(Bar bar, int htfBias)
    {
        _barIndex++;

        var events = new List<TrcEvent>();

        // Initialize on first bar
        if (_barIndex == 1)
        {
            _state.LastHigh = bar.High;
            _state.LastLow  = bar.Low;
            _state.Trend    = bar.Close > bar.Open ? Trend.Bull : Trend.Bear;
            return events;
        }

        // 1) Update structure and detect BOS / CHOCH
        var structEvt = UpdateStructure(bar);
        if (structEvt is not null)
        {
            events.Add(structEvt);

            // 2) Create retest setup only when CHOCH aligns with HTF bias
            if (structEvt.Type == EventType.ChochUp && htfBias == 1)
            {
                CreateRetestSetup(bar, direction: 1);
            }
            else if (structEvt.Type == EventType.ChochDown && htfBias == -1)
            {
                CreateRetestSetup(bar, direction: -1);
            }
        }

        // 3) Retest logic: TRC entries
        var trcEvt = CheckRetest(bar);
        if (trcEvt is not null)
            events.Add(trcEvt);

        return events;
    }

    // ─────────────────────────────
    // Structure logic (BOS / CHOCH)
    // ─────────────────────────────

    private TrcEvent? UpdateStructure(Bar bar)
    {
        var s = _state;
        TrcEvent? evt = null;

        // Bootstrap trend if needed
        if (s.Trend == Trend.None)
        {
            s.Trend = bar.Close > bar.Open ? Trend.Bull : Trend.Bear;
        }

        // Bullish internal trend
        if (s.Trend == Trend.Bull)
        {
            // CHOCH DOWN: close below last HL
            if (s.LastHl.HasValue && bar.Close < s.LastHl.Value)
            {
                s.Trend     = Trend.Bear;
                s.LastLh    = bar.High;
                s.LastHigh  = bar.High;
                s.LastLow   = bar.Low;

                evt = new TrcEvent(
                    bar.Time,
                    EventType.ChochDown,
                    new Dictionary<string, object?>
                    {
                        ["level"] = s.LastHl
                    });
            }
            else
            {
                // BOS UP: strong impulse high
                if (!s.LastHigh.HasValue || bar.High > s.LastHigh.Value)
                {
                    s.LastHigh = bar.High;
                    if (s.LastLow.HasValue)
                        s.LastHl = s.LastLow;

                    evt = new TrcEvent(
                        bar.Time,
                        EventType.BosUp,
                        new Dictionary<string, object?>
                        {
                            ["level"] = s.LastHigh
                        });
                }

                // Track lowest low since last high
                if (!s.LastLow.HasValue || bar.Low < s.LastLow.Value)
                    s.LastLow = bar.Low;
            }
        }
        // Bearish internal trend
        else if (s.Trend == Trend.Bear)
        {
            // CHOCH UP: close above last LH
            if (s.LastLh.HasValue && bar.Close > s.LastLh.Value)
            {
                s.Trend     = Trend.Bull;
                s.LastHl    = bar.Low;
                s.LastLow   = bar.Low;
                s.LastHigh  = bar.High;

                evt = new TrcEvent(
                    bar.Time,
                    EventType.ChochUp,
                    new Dictionary<string, object?>
                    {
                        ["level"] = s.LastLh
                    });
            }
            else
            {
                // BOS DOWN: new impulse low
                if (!s.LastLow.HasValue || bar.Low < s.LastLow.Value)
                {
                    s.LastLow = bar.Low;
                    if (s.LastHigh.HasValue)
                        s.LastLh = s.LastHigh;

                    evt = new TrcEvent(
                        bar.Time,
                        EventType.BosDown,
                        new Dictionary<string, object?>
                        {
                            ["level"] = s.LastLow
                        });
                }

                // Track highest high since last low
                if (!s.LastHigh.HasValue || bar.High > s.LastHigh.Value)
                    s.LastHigh = bar.High;
            }
        }

        return evt;
    }

    // ─────────────────────────────
    // Retest (C-leg) logic
    // ─────────────────────────────

    private void CreateRetestSetup(Bar bar, int direction)
    {
        var s = _state;

        s.SetupDir       = direction;
        s.BarsLeft       = _config.MaxRetestBars;
        s.SetupBarIndex  = _barIndex;

        if (direction == 1)
        {
            // Bullish CHOCH: zone between low and close
            s.ZoneLow  = bar.Low;
            s.ZoneHigh = bar.Close;
        }
        else
        {
            // Bearish CHOCH: zone between close and high
            s.ZoneLow  = bar.Close;
            s.ZoneHigh = bar.High;
        }
    }

    private TrcEvent? CheckRetest(Bar bar)
    {
        var s = _state;
        if (s.SetupDir == 0 || s.BarsLeft <= 0)
            return null;

        s.BarsLeft--;

        // Long setup: look for low retesting inside [zoneLow, zoneHigh]
        if (s.SetupDir == 1 && _barIndex > (s.SetupBarIndex ?? 0))
        {
            if (s.ZoneLow.HasValue && s.ZoneHigh.HasValue)
            {
                bool hit =
                    bar.Low <= s.ZoneHigh.Value &&
                    bar.Low >= s.ZoneLow.Value;

                if (hit)
                {
                    var evt = new TrcEvent(
                        bar.Time,
                        EventType.TrcLongEntry,
                        new Dictionary<string, object?>
                        {
                            ["entryZoneLow"]  = s.ZoneLow,
                            ["entryZoneHigh"] = s.ZoneHigh
                        });

                    s.ClearSetup();
                    return evt;
                }
            }
        }

        // Short setup: high retests inside [zoneLow, zoneHigh]
        if (s.SetupDir == -1 && _barIndex > (s.SetupBarIndex ?? 0))
        {
            if (s.ZoneLow.HasValue && s.ZoneHigh.HasValue)
            {
                bool hit =
                    bar.High >= s.ZoneLow.Value &&
                    bar.High <= s.ZoneHigh.Value;

                if (hit)
                {
                    var evt = new TrcEvent(
                        bar.Time,
                        EventType.TrcShortEntry,
                        new Dictionary<string, object?>
                        {
                            ["entryZoneLow"]  = s.ZoneLow,
                            ["entryZoneHigh"] = s.ZoneHigh
                        });

                    s.ClearSetup();
                    return evt;
                }
            }
        }

        // Expire setup if time runs out
        if (s.BarsLeft <= 0)
            s.ClearSetup();

        return null;
    }
}
