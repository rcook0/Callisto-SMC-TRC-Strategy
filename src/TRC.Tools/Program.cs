using TRC.App;
using TRC.Domain;
using TRC.Infra.Data;
using TRC.Infra.Execution;
using TRC.Infra.MarketData;

static class Cli
{
  public static async Task<int> Main(string[] args)
  {
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
      PrintHelp();
      return 0;
    }

    var cmd = args[0].ToLowerInvariant();
    var opts = ParseOptions(args.Skip(1).ToArray());

    if (cmd == "backtest")
      return await RunBacktestAsync(opts);

    Console.Error.WriteLine($"Unknown command: {cmd}");
    PrintHelp();
    return 2;
  }

  private static void PrintHelp()
  {
    Console.WriteLine("TRC.Tools");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  backtest --csv <path> --out <dir> [--symbol XAUUSD] [--smaLen 50] [--maxRetestBars 20]");
    Console.WriteLine();
  }

  private static Dictionary<string, string> ParseOptions(string[] args)
  {
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
      var a = args[i];
      if (!a.StartsWith("--")) continue;

      var key = a[2..];
      var val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
      d[key] = val;
    }
    return d;
  }

  private static string Get(Dictionary<string, string> d, string key, string def)
    => d.TryGetValue(key, out var v) ? v : def;

  private static int GetInt(Dictionary<string, string> d, string key, int def)
    => int.TryParse(Get(d, key, def.ToString()), out var v) ? v : def;

  private static async Task<int> RunBacktestAsync(Dictionary<string, string> opts)
  {
    var csv = Get(opts, "csv", "");
    var outDir = Get(opts, "out", "out");
    var symbol = Get(opts, "symbol", "SYMBOL");
    var smaLen = GetInt(opts, "smaLen", 50);
    var maxRetestBars = GetInt(opts, "maxRetestBars", 20);

    if (string.IsNullOrWhiteSpace(csv) || !File.Exists(csv))
    {
      Console.Error.WriteLine("Missing or invalid --csv path.");
      return 2;
    }

    Directory.CreateDirectory(outDir);

    var bars = new CsvBarSource(csv);
    var bias = new HtfBiasServiceSma(smaLen);

    await using var eventSink = new CsvEventSink(Path.Combine(outDir, "events.csv"));
    await using var tradeSink = new CsvTradeSink(Path.Combine(outDir, "trades.csv"));

    var exec = new PaperExecutionModel();

    var runner = new BacktestRunner(bars, bias, eventSink, tradeSink, exec);

    var req = new BacktestRequest(
      Symbol: symbol,
      TrcConfig: new TrcConfig(MaxRetestBars: maxRetestBars)
    );

    Console.WriteLine($"Running backtest: symbol={symbol} csv={csv}");
    var res = await runner.RunAsync(req, CancellationToken.None);

    await SummaryWriter.WriteAsync(Path.Combine(outDir, "summary.json"), res, CancellationToken.None);

    Console.WriteLine($"Done. Bars={res.BarsProcessed} Events={res.EventsWritten} Trades={res.TradesWritten}");
    Console.WriteLine($"Outputs: {outDir}/events.csv, {outDir}/trades.csv, {outDir}/summary.json");
    return 0;
  }
}

return await Cli.Main(args);
