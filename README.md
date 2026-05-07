# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, explicit macro interpretations, weekly review notes, and trade idea journal entries.

The system is a **factor monitor**, not a fixed scenario classifier:

```text
Raw data -> measurable factor scores -> macro interpretation -> trade candidates
```

> Scope note: this is a manual monitoring and journaling app only. It does not include broker integration, automatic trading, execution routing, or order management.

## Features

- Blazor dashboard showing the latest persisted factor scores from SQLite.
- EF Core `DbContext` backed by SQLite.
- Six measurable macro factors are preserved as the base scoring layer:
  1. Inflation Pressure
  2. Inflation Breadth
  3. Energy Shock
  4. Growth Stress
  5. Fiscal/Treasury Stress
  6. Market Complacency
- Explicit derived macro interpretations from those six factors:
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

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

A measurable factor score is therefore the data-backed layer of the dashboard: one observed indicator is compared with its baseline and volatility, direction-adjusted, and weighted by the factor's configured importance. The factor table also shows a pressure contribution so the user can see whether each scored factor is adding macro pressure, providing relief, or remaining balanced.

Dashboard factor impact wording uses pressure-aware labels: `Pressure rising`, `Mild pressure`, `Relief`, `Balanced`, and the Market Complacency-specific `Complacency pressure`.

## Macro interpretation approach

The dashboard keeps the six measurable factor scores visible and then derives interpretation readings from them:

| Interpretation | Derived from |
| --- | --- |
| inflation/stagflation pressure | Inflation Pressure, Inflation Breadth, Energy Shock |
| fiscal/Treasury stress | Fiscal/Treasury Stress |
| hard-landing pressure | Growth Stress |
| market complacency/mispricing | Market Complacency |

Each macro pressure interpretation aggregates only the measurable factors that were actually scored for that interpretation, then explains why those factor-level pressure contributions support the card reading. These readings are decision-support context for a human user. They are not generic-only regime labels, not trading signals, and not automated execution instructions.

Market Complacency is intentionally handled as a special case. A low-volatility reading can indicate that markets are underpricing macro risks, so the dashboard shows it as market complacency/mispricing pressure instead of a bullish signal.

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
