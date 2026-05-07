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
- Trade idea journal page with entry trigger, invalidation, catalyst, max loss, time horizon, risk notes, and post-mortem fields.
- Startup-time SQLite schema upgrade for local databases created by earlier versions of the app.

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

## Local SQLite schema upgrades

The app uses `EnsureCreatedAsync` for the lightweight local SQLite store and then runs a startup schema upgrade step. Existing `macro-regime.db` files created before v0.3 are upgraded in place by adding the trade-journal columns for entry trigger, invalidation, catalyst, max loss, time horizon, and post-mortem notes when those columns are missing.

To apply the upgrade locally:

1. Stop any running instance of the app.
2. Back up `macro-regime.db` if you want a rollback point.
3. Pull the latest branch and run the app again:

   ```powershell
   dotnet run --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
   ```

The startup upgrade is idempotent, so re-running the app will not duplicate columns.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score. The dashboard then derives an interpretation layer from the persisted scores, including a risk posture, primary macro drag, primary macro support, and weekly review focus.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
