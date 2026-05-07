# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core.

> Scope note: this is a monitoring and journaling app only. It does not include broker integration or automatic trading.

## Features

- Blazor dashboard showing the latest persisted measurable factor scores and derived macro pressure interpretations.
- EF Core `DbContext` backed by SQLite.
- Seed data for six initial macro factors:
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

On first startup, the app creates a local SQLite database file named `macro-regime.db` in the working directory and seeds it with the initial macro factors, one indicator per factor, latest observations, factor scores, a sample weekly review, and a sample trade idea.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

The six measurable factors remain:

1. Inflation Pressure
2. Inflation Breadth
3. Energy Shock
4. Growth Stress
5. Fiscal/Treasury Stress
6. Market Complacency

## Factor scores vs. macro pressure interpretations

The dashboard separates three ideas that can otherwise be confused:

1. **Raw factor scoring direction**: the normalized score produced from an indicator's value, baseline, volatility, and `HigherIsRiskOn` setting.
2. **Macro pressure interpretation**: a pressure-or-relief mapping used by the monitor to decide whether a factor is contributing to macro pressure.
3. **Dashboard wording**: user-facing labels such as `Pressure rising`, `Mild pressure`, `Balanced`, `Relief`, and `Complacency pressure`.

This app is a factor monitor, not a fixed scenario classifier. The four derived macro interpretations are:

- Inflation/stagflation pressure
- Fiscal/Treasury stress
- Hard-landing pressure
- Market complacency/mispricing

For interpretation scoring, inflation pressure, inflation breadth, energy shock, growth stress, and fiscal/Treasury stress add pressure when their weighted factor score is negative. Market Complacency is intentionally different: low volatility can be good for risk assets in a narrow market sense, but this monitor treats unusually low volatility as potential complacency or mispricing pressure when the factor score indicates VIX is below its baseline. That prevents low VIX from appearing as generic support when the intended macro interpretation is building complacency/mispricing pressure.

## Scope and trading limitations

This project has no broker integration, order routing, execution workflow, or automatic trading. Trade ideas are journal entries only, and dashboard readings are monitoring inputs rather than trading instructions.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
