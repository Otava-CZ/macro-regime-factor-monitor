# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro factor interpretation scores, weekly review notes, and trade idea journal entries.

The app keeps the original weighted scoring idea, but stores factors, indicators, observations, scores, weekly reviews, and trade ideas in SQLite through EF Core.

> Scope note: this is a monitoring and journaling app only. It does not include broker integration, order routing, or automatic trading.

## Features

- Blazor dashboard showing the latest persisted factor scores.
- EF Core `DbContext` backed by SQLite.
- Seed data for four macro factor interpretations:
  1. inflation/stagflation pressure
  2. fiscal/Treasury stress
  3. hard-landing pressure
  4. market complacency/mispricing
- Simple weekly review page.
- Trade idea journal page with thesis, entry trigger, invalidation, catalyst, max loss, time horizon, risk notes, and post-mortem fields.
- GitHub Actions PR workflow for .NET 8 restore/build checks on pull requests targeting `develop` or `main`.

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

Version 0.3 adds these `TradeIdea` fields: `EntryTrigger`, `Invalidation`, `Catalyst`, `MaxLoss`, `TimeHorizon`, and `PostMortem`. At startup, the app safely checks existing local SQLite databases and adds any missing columns with empty-string defaults before seeding or reading data. Existing seeded rows and locally entered trade ideas remain usable.

If you prefer a clean dev database, stop the app, delete `macro-regime.db`, and run the app again to recreate it with the v0.3 seed data.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into macro interpretation scores and a composite dashboard score.

## Macro interpretation labels

The dashboard intentionally avoids generic regime names. It groups factor scores into these interpretations and highlights the interpretation with the largest absolute score as the dominant macro interpretation:

| Interpretation | Purpose |
| --- | --- |
| inflation/stagflation pressure | Sticky inflation, energy, or commodity pressure that tightens the policy trade-off. |
| fiscal/Treasury stress | Treasury-market or fiscal financing stress. |
| hard-landing pressure | Growth and labor-market downside pressure. |
| market complacency/mispricing | Risk pricing that appears too relaxed or misaligned with macro risks. |
