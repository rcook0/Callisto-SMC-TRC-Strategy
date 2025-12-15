namespace TRC.Core;

public enum Trend
{
    None = 0,
    Bull = 1,
    Bear = -1
}

public enum EventType
{
    None = 0,
    BosUp,
    BosDown,
    ChochUp,
    ChochDown,
    TrcLongEntry,
    TrcShortEntry
}
