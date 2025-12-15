namespace TRC.Core;

/// <summary>
/// Internal mutable state for TRC engine.
/// </summary>
public sealed class TrcState
{
    // 5m structure state
    public Trend Trend { get; set; } = Trend.None;
    public decimal? LastHigh { get; set; }
    public decimal? LastLow  { get; set; }
    public decimal? LastHl   { get; set; } // last Higher Low (bull)
    public decimal? LastLh   { get; set; } // last Lower High (bear)

    // Retest setup state
    public int SetupDir { get; set; } = 0; // 1 = long, -1 = short, 0 = none
    public decimal? ZoneLow  { get; set; }
    public decimal? ZoneHigh { get; set; }
    public int BarsLeft { get; set; } = 0;
    public int? SetupBarIndex { get; set; }

    public void ClearSetup()
    {
        SetupDir = 0;
        ZoneLow = null;
        ZoneHigh = null;
        BarsLeft = 0;
        SetupBarIndex = null;
    }
}
