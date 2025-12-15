using System.Globalization;
using TRC.App;
using TRC.Domain;

namespace TRC.Infra.Data;

public sealed class CsvEventSink : IEventSink, IAsyncDisposable
{
  private readonly StreamWriter _sw;
  private bool _headerWritten;

  public CsvEventSink(string path)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    _sw = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));
  }

  public async ValueTask WriteAsync(TrcEvent evt, CancellationToken ct)
  {
    if (!_headerWritten)
    {
      await _sw.WriteLineAsync("timeUtc,type,info").ConfigureAwait(false);
      _headerWritten = true;
    }

    var info = SerializeInfo(evt.Info);
    var line = $"{evt.TimeUtc:O},{evt.Type},{Escape(info)}";
    await _sw.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
  }

  public async ValueTask FlushAsync(CancellationToken ct)
  {
    await _sw.FlushAsync().ConfigureAwait(false);
  }

  private static string SerializeInfo(IReadOnlyDictionary<string, object?> info)
  {
    // compact key=value pairs
    return string.Join(";", info.Select(kvp => $"{kvp.Key}={kvp.Value}"));
  }

  private static string Escape(string s)
  {
    // Minimal CSV escaping: wrap with quotes and double internal quotes if needed
    if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
      return """ + s.Replace(""", """") + """;
    return s;
  }

  public async ValueTask DisposeAsync()
  {
    await _sw.DisposeAsync().ConfigureAwait(false);
  }
}
