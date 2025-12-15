namespace TRC.Domain;

public enum Trend
{
  None = 0,
  Bull = 1,
  Bear = -1
}

public enum TrcEventType
{
  None = 0,

  // Structure events
  BosUp,
  BosDown,
  ChochUp,
  ChochDown,

  // Strategy events
  TrcLongSetup,
  TrcShortSetup,
  TrcLongEntry,
  TrcShortEntry
}
