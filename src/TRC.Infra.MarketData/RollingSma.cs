using System.Collections.Generic;

namespace TRC.Infra.MarketData;

/// <summary>
/// Rolling SMA for decimals.
/// </summary>
public sealed class RollingSma
{
  private readonly int _len;
  private readonly Queue<decimal> _q = new();
  private decimal _sum;

  public RollingSma(int len)
  {
    if (len <= 0) throw new ArgumentOutOfRangeException(nameof(len));
    _len = len;
  }

  public int Count => _q.Count;

  public decimal? Current { get; private set; }

  public decimal? Push(decimal x)
  {
    _q.Enqueue(x);
    _sum += x;

    if (_q.Count > _len)
      _sum -= _q.Dequeue();

    if (_q.Count == _len)
      Current = _sum / _len;

    return Current;
  }
}
