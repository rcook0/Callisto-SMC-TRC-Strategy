using TRC.App;
using TRC.Domain;
using TRC.Infra.Data;
using TRC.Infra.Execution;
using TRC.Infra.MarketData;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

// NOTE: This is an internal/dev endpoint scaffolding.
// In a real deployment you'd upload data or fetch from storage rather than reading arbitrary file paths.
app.MapPost("/backtests/run", async (BacktestRunRequest req) =>
{
  if (string.IsNullOrWhiteSpace(req.CsvPath) || !File.Exists(req.CsvPath))
    return Results.BadRequest(new { error = "CsvPath missing or file not found." });

  var outDir = string.IsNullOrWhiteSpace(req.OutDir) ? "out" : req.OutDir;
  Directory.CreateDirectory(outDir);

  var bars = new CsvBarSource(req.CsvPath);
  var bias = new HtfBiasServiceSma(req.SmaLen);

  await using var eventSink = new CsvEventSink(Path.Combine(outDir, "events.csv"));
  await using var tradeSink = new CsvTradeSink(Path.Combine(outDir, "trades.csv"));

  var exec = new PaperExecutionModel();
  var runner = new BacktestRunner(bars, bias, eventSink, tradeSink, exec);

  var btReq = new BacktestRequest(
    Symbol: req.Symbol ?? "SYMBOL",
    TrcConfig: new TrcConfig(MaxRetestBars: req.MaxRetestBars)
  );

  var result = await runner.RunAsync(btReq, CancellationToken.None);
  await SummaryWriter.WriteAsync(Path.Combine(outDir, "summary.json"), result, CancellationToken.None);

  return Results.Ok(result);
});

app.Run();

public sealed record BacktestRunRequest(
  string CsvPath,
  string? Symbol,
  string? OutDir,
  int SmaLen = 50,
  int MaxRetestBars = 20
);
