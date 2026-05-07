# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, explicit macro pressure interpretations, weekly review notes, and trade idea journal entries.

The system is a **factor monitor**, not a fixed scenario classifier:

```text
Raw data -> measurable factor scores -> macro interpretation -> trade candidates
```

> Scope note: this is a manual monitoring and journaling app only. It does not include broker integration, automatic trading, execution routing, or order management.

## Features

- Blazor dashboard showing the latest persisted six measurable factor scores from SQLite.
- EF Core `DbContext` backed by SQLite.
- Six measurable macro factors are preserved as the base scoring layer:
  1. Inflation Pressure
  2. Inflation Breadth
  3. Energy Shock
  4. Growth Stress
  5. Fiscal/Treasury Stress
  6. Market Complacency
- Explicit derived macro pressure interpretations from those six factors:
  - inflation/stagflation pressure
  - fiscal/Treasury stress
  - hard-landing pressure
  - market complacency/mispricing
- Weekly review page for manual macro notes.
- Trade idea journal page with fields for thesis, entry trigger, invalidation, catalyst, max loss, time horizon, risk notes, and post-mortem.
- Startup SQLite schema upgrade that adds v0.3 trade idea columns to existing local `macro-regime.db` files.

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

If a local database already exists from an earlier version, startup applies a lightweight SQLite schema upgrade before querying trade ideas so the v0.3 journal fields are available without deleting local data.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher indicator values are constructive for raw risk-asset scoring and `-1` when higher indicator values are adverse for raw risk-asset scoring. The raw and weighted factor scores are preserved as the measurable scoring layer. Weighted scores are still summed into category scores and a composite dashboard score for continuity.

## Factor score vs. macro pressure interpretation

A measurable factor score is not always the same thing as the dashboard's macro pressure interpretation. The factor score answers, "How did this observed indicator score under its raw direction and weight?" The interpretation layer answers, "Does this factor add macro pressure or macro relief for the monitor's current theme?"

The dashboard therefore uses an explicit pressure mapping instead of relying only on `HigherIsRiskOn` wording:

| Measurable factor | Pressure contribution |
| --- | --- |
| Inflation Pressure | Higher pressure when the weighted score is negative |
| Inflation Breadth | Higher pressure when the weighted score is negative |
| Energy Shock | Higher pressure when the weighted score is negative |
| Growth Stress | Higher hard-landing pressure when the weighted score is negative |
| Fiscal/Treasury Stress | Higher pressure when the weighted score is negative |
| Market Complacency | Higher complacency/mispricing pressure when the factor score indicates unusually calm volatility |

This distinction is especially important for Market Complacency. Low volatility can be supportive for near-term risk assets, but the macro monitor treats volatility below its baseline as possible complacency or mispricing pressure because risk pricing may be too relaxed versus macro risks. The dashboard should therefore describe low-VIX complacency as complacency/mispricing pressure, not as generic support.

## Macro interpretation approach

The dashboard keeps the six measurable factor scores visible and then derives four interpretation readings from them. Each interpretation displays its score, reading, contributing measurable factors, and a short "Why this interpretation?" explanation.

| Interpretation | Derived from |
| --- | --- |
| inflation/stagflation pressure | Inflation Pressure, Inflation Breadth, Energy Shock |
| fiscal/Treasury stress | Fiscal/Treasury Stress |
| hard-landing pressure | Growth Stress |
| market complacency/mispricing | Market Complacency |

These readings are decision-support context for a human user. They are not generic-only regime labels, not trading signals, and not automated execution instructions. The app has no broker integration, no automatic trading, no execution routing, and no order management.

## Composite regime thresholds

The app still computes a secondary composite regime label for continuity, but the main dashboard interpretation is derived macro pressure from the measurable factors.

| Composite score | Secondary label |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |

## Continuous integration

The repository includes one GitHub Actions workflow that restores and builds the .NET 8 solution on pushes and pull requests targeting `develop` or `main`.
