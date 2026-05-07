# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core.

> Scope note: this is a monitoring and journaling app only. It does not include broker integration or automatic trading.

## Features

- Blazor dashboard showing the latest persisted factor scores.
- EF Core `DbContext` backed by SQLite.
- Seed data for six initial macro factors:
  1. Inflation Pressure
  2. Inflation Breadth
  3. Energy Shock
  4. Growth Stress
  5. Fiscal/Treasury Stress
  6. Market Complacency
- Simple weekly review page.
- Trade idea journal page with thesis, entry trigger, invalidation, catalyst, max-loss, time-horizon, risk-notes, and post-mortem fields.

## Development environment

This repository includes a Dev Container definition that uses the official .NET 8 SDK image:

```json
"image": "mcr.microsoft.com/devcontainers/dotnet:8.0"
```

Open the repository in the Dev Container so `dotnet` is available for restore, build, and run commands.

## Run on Windows

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Open PowerShell in the repository root.
3. Restore and build the solution:

   ```powershell
   dotnet restore
   dotnet build
   ```

4. Run the Blazor app:

   ```powershell
   dotnet run --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
   ```

5. Open the local URL printed by `dotnet run`, such as `https://localhost:5001` or `http://localhost:5000`.

On first startup, the app creates a local SQLite database file named `macro-regime.db` in the working directory and seeds it with the initial macro factors, one indicator per factor, latest observations, factor scores, a sample weekly review, and a sample trade idea.

## Local SQLite upgrades

The app uses `EnsureCreated` for lightweight local development databases rather than a production migration workflow. On startup, after the SQLite database is created or opened, the app runs a small compatibility upgrade that inspects the existing `TradeIdeas` table and adds any missing v0.3 journal columns:

- `EntryTrigger`
- `Invalidation`
- `Catalyst`
- `MaxLoss`
- `TimeHorizon`
- `PostMortem`

Existing rows are preserved and receive empty-string defaults for newly added journal fields. If you want to rebuild all seed data from scratch instead, stop the app, delete the local `macro-regime.db` file in your run directory, and start the app again.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

## Macro interpretation layer

The dashboard keeps the six measurable factor scores as the source of truth and derives four higher-level interpretation readings from those persisted scores:

1. inflation/stagflation pressure
2. fiscal/Treasury stress
3. hard-landing pressure
4. market complacency/mispricing

These derived readings are meant to summarize the six-factor model; they do not replace the measurable factors.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
