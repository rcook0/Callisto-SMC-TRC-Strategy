namespace TRC.Domain;

/// <summary>
/// Mutable state for TRC, suitable for per-bar updates.
/// </summary>
public sealed class TrcState
{
  // 5m structure tracking
  public Trend Trend { get; set; } = Trend.None;

  // Structural extremes (impulse points)
  public decimal? LastHigh { get; set; }
  public decimal? LastLow { get; set; }

  // Confirmed structure pivots
  public decimal? LastHL { get; set; }  // higher low (bull)
  public decimal? LastLH { get; set; }  // lower high (bear)

  // Incremental pullback candidates
  public decimal? PendingLow { get; set; }   // min since last impulse high (bull)
  public decimal? PendingHigh { get; set; }  // max since last impulse low (bear)

  // Retest setup state (TRC C-leg)
  public int SetupDir { get; set; } = 0; // 1 long, -1 short
  public decimal? ZoneLow { get; set; }
  public decimal? ZoneHigh { get; set; }
  public int BarsLeft { get; set; } = 0;
  public long SetupSeq { get; set; } = 0; // increments each setup (useful for correlation)
  public long SetupStartedBarIndex { get; set; } = 0;

  public void ClearSetup()
  {
    SetupDir = 0;
    ZoneLow = null;
    ZoneHigh = null;
    BarsLeft = 0;
    SetupStartedBarIndex = 0;
  }
}
