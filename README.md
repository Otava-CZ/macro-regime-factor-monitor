# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core.

> Scope note: this is a monitoring and journaling app only. It does not include broker integration or automatic trading.

## Features

- Blazor dashboard showing the latest persisted factor scores and four derived macro pressure interpretations.
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
- Explicit pressure polarity mapping so dashboard language distinguishes measurable factor direction from macro-monitor interpretation.

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

The monitor separates three related concepts:

1. **Measurable factor score**: the normalized raw and weighted score for one observable factor.
2. **Macro pressure interpretation**: a polarity-aware conversion from factor score into pressure or relief for the macro monitor.
3. **Dashboard wording**: user-facing labels such as `Pressure rising`, `Mild pressure`, `Balanced`, `Relief`, or `Complacency pressure`.

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Raw and weighted scores remain factor scores; they are not always the final macro-pressure reading shown to the user.

Weighted scores are summed into category scores and a composite dashboard score. Separately, the dashboard derives four macro interpretations:

- inflation/stagflation pressure
- fiscal/Treasury stress
- hard-landing pressure
- market complacency/mispricing

For inflation, energy, growth stress, and fiscal/Treasury factors, negative weighted scores represent pressure and positive weighted scores represent relief. Market Complacency is intentionally different: because the current VIX-based factor scores low volatility as a positive raw factor move, a positive Market Complacency score is converted into **market complacency/mispricing pressure** rather than generic support. Low volatility can be calm for risk assets, but in this monitor it may also indicate underpriced macro risk or complacency versus the factor baseline.

The app remains a factor monitor and journal, not a fixed scenario classifier. It does not connect to brokers, place orders, or perform automatic trading.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
