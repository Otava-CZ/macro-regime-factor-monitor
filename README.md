# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, explicit macro pressure interpretations, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core. On startup it creates the database when needed and applies a lightweight SQLite schema upgrade for journal fields added after the initial prototype.

> Scope note: this is a monitoring and journaling app only. It does not include broker integration, order routing, automatic trading, or any automated execution workflow. Trade ideas are manual review records.

## Features

- Blazor dashboard showing the latest persisted factor scores.
- EF Core `DbContext` backed by SQLite.
- Seed data for six measurable macro factors:
  1. Inflation Pressure
  2. Inflation Breadth
  3. Energy Shock
  4. Growth Stress
  5. Fiscal/Treasury Stress
  6. Market Complacency
- Explicit derived macro pressure interpretations for inflation/stagflation, growth/downturn, fiscal/Treasury, and market complacency/mispricing pressure.
- Pressure-aware dashboard wording that distinguishes risk-asset factor scores from macro pressure polarity.
- Weekly review page.
- Expanded manual trade idea journal page with macro regime, pressure thesis, time horizon, entry trigger, exit plan, and risk notes.
- One GitHub Actions workflow that restores, builds, and tests changes targeting `develop`.

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
   dotnet restore MacroRegimeFactorMonitor.sln
   dotnet build MacroRegimeFactorMonitor.sln --configuration Release --no-restore
   ```

4. Run the Blazor app:

   ```powershell
   dotnet run --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
   ```

5. Open the local URL printed by `dotnet run`, such as `https://localhost:5001` or `http://localhost:5000`.

On first startup, the app creates a local SQLite database file named `macro-regime.db` in the working directory and seeds it with the initial macro factors, one indicator per factor, latest observations, factor scores, a sample weekly review, and a sample manual trade idea.

## Factor score vs macro pressure interpretation

The dashboard intentionally shows two related but different concepts:

1. **Factor score**: a normalized risk-asset support score. Positive values generally mean the observation is constructive for risk assets, while negative values generally mean it is a risk-off drag.
2. **Macro pressure interpretation**: a derived pressure reading that remaps each factor into an explicit macro-pressure polarity. For most stress factors, negative risk-asset scores add macro pressure. For Market Complacency, positive risk-asset scores caused by low volatility or relaxed risk pricing add market complacency/mispricing pressure instead of being described as generic support.

This means a low-volatility Market Complacency reading can improve the raw risk-asset score while still increasing the derived market complacency/mispricing pressure interpretation.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

## Pressure polarity mapping

| Factor | Pressure interpretation | Polarity |
| --- | --- | --- |
| Inflation Pressure | Inflation/stagflation pressure | Negative factor scores increase macro pressure; positive scores ease pressure. |
| Inflation Breadth | Inflation/stagflation pressure | Negative factor scores increase macro pressure; positive scores ease pressure. |
| Energy Shock | Inflation/stagflation pressure | Negative factor scores increase macro pressure; positive scores ease pressure. |
| Growth Stress | Growth/downturn pressure | Negative factor scores increase macro pressure; positive scores ease pressure. |
| Fiscal/Treasury Stress | Fiscal/Treasury pressure | Negative factor scores increase macro pressure; positive scores ease pressure. |
| Market Complacency | Market complacency/mispricing pressure | Positive factor scores increase macro pressure; negative scores ease pressure. |

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
