# TRC Platform (C# / .NET 8)

This repository is a long-term, modular foundation for implementing TRC (Trend → Reversal → Continuation) as a **canonical, testable, reusable** strategy engine, with adapters for data, execution, APIs, and UI.

## Design goals

- **Single source of truth**: the strategy engine lives in `TRC.Domain` as a per-bar state machine.
- **Replayable**: backtests and live runs should use the same event pipeline.
- **Replaceable plumbing**: UI, broker adapters, data stores can change without rewriting strategy logic.
- **Explainable**: every signal should be audit-able via emitted events.

## Solution layout

- `TRC.Domain` — pure domain model: bars, structure tracking (BOS/CHoCH), TRC engine, events.
- `TRC.App` — use-cases: backtest runner, live session orchestrators (scaffolded).
- `TRC.Infra.MarketData` — CSV ingest + timeframe aggregation (scaffolded, functional for CSV backtest).
- `TRC.Infra.Execution` — paper execution model (scaffolded).
- `TRC.Infra.Data` — CSV sinks for events/trades (scaffolded, functional).
- `TRC.Tools` — CLI runner (`trc-backtest`) for research & regression.
- `TRC.Api` — ASP.NET Core Minimal API (scaffold, placeholder endpoints).
- `TRC.UI` — Blazor Server UI (scaffold, placeholder).
- `TRC.Tests` — xUnit tests for domain invariants & golden-path sequences.

## Build & run (VS / CLI)

1) Install .NET SDK 8.x

2) Build:

```bash
dotnet restore
dotnet build -c Release
```

3) Run backtest (CSV):

```bash
dotnet run --project src/TRC.Tools -- backtest --csv "data/sample_5m.csv" --out "out"
```

Outputs:
- `out/events.csv`
- `out/trades.csv`
- `out/summary.json`

CSV format expected (header required):

```text
time,open,high,low,close,volume
2025-01-01T09:00:00Z,100,101,99,100.5,123
...
```

## Notes on faithfulness (ICT/SMC)

- **BOS** is treated as trend continuation (new HH in bull, new LL in bear).
- **CHoCH** is treated as initial shift in order flow: in bull, close below last HL; in bear, close above last LH.
- Swings are maintained **incrementally** (no fixed 2-2 / 3-3 windows) to match the “price takes the time it needs” viewpoint.

## Next roadmap steps (already prepared by structure)

- Replace MA-based HTF bias in the backtest runner with HTF structure-based bias.
- Add OB/FVG/liquidity modules as independent detectors under `TRC.Domain.Smc`.
- Add storage adapters (Timescale/Postgres, Parquet) behind `TRC.App` ports.
- Add broker adapters (MT5/cTrader) behind `TRC.Infra.Execution`.

