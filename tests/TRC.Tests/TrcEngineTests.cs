using TRC.Domain;
using Xunit;

namespace TRC.Tests;

public sealed class TrcEngineTests
{
  [Fact]
  public void Emits_CHOCH_setup_and_entry_for_bull_reversal()
  {
    var engine = new TrcEngine(new TrcConfig(MaxRetestBars: 10));

    var t0 = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    // Bar1: bearish bootstrap
    var b1 = new Bar(t0, 100m, 101m, 99m, 99.5m, 0m);
    Assert.Empty(engine.OnBar(b1, htfBias: 0));

    // Bar2: pullback high (creates pendingHigh)
    var b2 = new Bar(t0.AddMinutes(5), 99.5m, 102m, 99m, 101.5m, 0m);
    var e2 = engine.OnBar(b2, htfBias: 0);
    // maybe BOS/none; don't assert

    // Bar3: BOS down confirms LH
    var b3 = new Bar(t0.AddMinutes(10), 101m, 101m, 98m, 98.5m, 0m);
    var e3 = engine.OnBar(b3, htfBias: 0);
    Assert.Contains(e3, e => e.Type == TrcEventType.BosDown);

    // Bar4: close above LH => CHOCH up; with HTF bias bullish => long setup
    var b4 = new Bar(t0.AddMinutes(15), 98.5m, 104m, 98.5m, 103.2m, 0m);
    var e4 = engine.OnBar(b4, htfBias: 1);

    Assert.Contains(e4, e => e.Type == TrcEventType.ChochUp);
    Assert.Contains(e4, e => e.Type == TrcEventType.TrcLongSetup);

    // Bar5: retest into zone triggers entry
    var b5 = new Bar(t0.AddMinutes(20), 103.2m, 103.5m, 101m, 102m, 0m);
    var e5 = engine.OnBar(b5, htfBias: 1);

    Assert.Contains(e5, e => e.Type == TrcEventType.TrcLongEntry);
  }
}
