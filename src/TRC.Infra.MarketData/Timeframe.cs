namespace TRC.Infra.MarketData;

public enum Timeframe
{
  M5,
  M15,
  H1,
  H4
}

public static class TimeframeExtensions
{
  public static TimeSpan ToTimeSpan(this Timeframe tf) => tf switch
  {
    Timeframe.M5  => TimeSpan.FromMinutes(5),
    Timeframe.M15 => TimeSpan.FromMinutes(15),
    Timeframe.H1  => TimeSpan.FromHours(1),
    Timeframe.H4  => TimeSpan.FromHours(4),
    _ => throw new ArgumentOutOfRangeException(nameof(tf))
  };
}
