# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, derived macro interpretations, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core.

> Strategic rule: this is a factor monitor, not a fixed scenario classifier. The intended flow is raw data -> measurable factor scores -> macro interpretation -> trade candidates.
>
> Scope note: this is a monitoring and journaling app only. It does not include broker integration or automatic trading.

## Features

- Blazor dashboard showing the latest persisted measurable factor scores.
- Dashboard interpretation layer deriving four macro interpretations from the six measurable factors:
  1. Inflation/stagflation pressure
  2. Fiscal/Treasury stress
  3. Hard-landing pressure
  4. Market complacency/mispricing
- EF Core `DbContext` backed by SQLite.
- Seed data for six initial measurable macro factors:
  1. Inflation Pressure
  2. Inflation Breadth
  3. Energy Shock
  4. Growth Stress
  5. Fiscal/Treasury Stress
  6. Market Complacency
- Simple weekly review page.
- Simple trade idea journal page.

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

On first startup, the app creates a local SQLite database file named `macro-regime.db` in the working directory and seeds it with the initial measurable macro factors, one indicator per factor, latest observations, factor scores, a sample weekly review, and a sample trade idea.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for the monitored factor signal or reduce the monitored pressure, and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

The interpretation layer does not overwrite or rename the factor records. It maps the measurable factor scores into a separate dashboard view:

| Macro interpretation | Source measurable factors |
| --- | --- |
| Inflation/stagflation pressure | Inflation Pressure, Inflation Breadth, Energy Shock, Growth Stress |
| Fiscal/Treasury stress | Fiscal/Treasury Stress |
| Hard-landing pressure | Growth Stress |
| Market complacency/mispricing | Market Complacency |

Interpretation pressure scores are displayed as the inverse of the mapped weighted factor contribution so higher positive interpretation values mean more macro pressure, while negative values mean relief.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
