namespace TRC.Domain;

public sealed record TrcConfig(
  int MaxRetestBars = 20,
  bool RequireCloseBeyondStructure = true
);

public sealed record TrcEvent(
  DateTime TimeUtc,
  TrcEventType Type,
  IReadOnlyDictionary<string, object?> Info
);
