# Macro Regime Factor Monitor

A small .NET 8 ASP.NET Core Blazor app for tracking macro regime factor scores, explicit macro interpretations, weekly review notes, and trade idea journal entries.

The system is a **factor monitor**, not a fixed scenario classifier:

```text
Raw data -> measurable factor scores -> macro interpretation -> trade candidates
```

> Scope note: this is a manual monitoring and journaling app only. It does not include broker integration, automatic trading, execution routing, or order management.

## Features

- Blazor dashboard showing the latest persisted factor scores from the configured EF Core database.
- EF Core `DbContext` backed by local SQLite by default, with PostgreSQL/Supabase support for development environments.
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
- Startup SQLite schema upgrade that adds v0.3 trade idea columns to existing local `macro-regime.db` files; PostgreSQL uses EF-created schema initialization for new databases.

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

## Database configuration

The app chooses its EF Core provider from configuration:

- `Database:Provider = Postgres` uses the Npgsql PostgreSQL provider with `ConnectionStrings:MacroRegime`.
- Any other value, a blank value, or a missing provider uses SQLite.
- If SQLite is selected and `ConnectionStrings:MacroRegime` is blank or missing, the app falls back to the local SQLite connection string `Data Source=macro-regime.db`.
- If Postgres is selected, `ConnectionStrings:MacroRegime` must be set. The app fails fast if it is missing or blank.

`appsettings.json` intentionally does not contain a real database secret. Keep the local default on SQLite for normal development, and store any Supabase/Postgres connection string outside source control with .NET user secrets.

### Local SQLite default

No configuration is required for the default local SQLite path:

```powershell
dotnet run --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
```

You may also set the provider explicitly while leaving the connection string blank so the built-in local fallback is used:

```powershell
dotnet user-secrets init --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
dotnet user-secrets set "Database:Provider" "Sqlite" --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
```

### Supabase/Postgres development

For Supabase development, set the provider to `Postgres` and store the connection string with user secrets. Replace the host, database, user, and placeholder password with your Supabase project values; do not commit the real password.

```powershell
dotnet user-secrets init --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
dotnet user-secrets set "Database:Provider" "Postgres" --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
dotnet user-secrets set "ConnectionStrings:MacroRegime" "Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<placeholder-password>;SSL Mode=Require;Trust Server Certificate=true" --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
```

The startup path creates the EF model schema for a new database before seeding the sample monitor data. The legacy lightweight column upgrade remains SQLite-only and is not run against Postgres.

## Historical data ingestion plan

Historical macro data ingestion is intended to be source-driven rather than copy/pasted by hand. The database foundation added for future import work separates provider metadata, app-to-provider series mappings, audited import attempts, and normalized observations.

- `DataSources` defines official/public providers such as FRED, BLS, EIA, and Treasury Fiscal Data, including provider type, base URL, whether an API key is expected, and operational notes. No API keys or secrets are stored in source control.
- `ExternalSeries` will map one app `Indicator` to one provider series or endpoint, including the provider series id, endpoint, frequency, units, transform, and response fields used for observation dates and values.
- `DataImportRuns` will audit each import attempt with its source, start/finish timestamps, status, row counts, errors, and notes.
- `IndicatorObservations` stores normalized observations used by the app and now includes nullable source/import tracking fields for external series, import runs, source names, release dates, vintage dates, and timestamps.

Future PRs will add importer clients for official/public APIs, including the FRED observations API, BLS public timeseries API, EIA API v2, and Treasury Fiscal Data REST API. This PR does not implement live API fetching, does not import historical datasets, and does not add API keys.

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
