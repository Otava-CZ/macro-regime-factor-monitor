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
- Startup SQLite schema upgrade that adds v0.3 trade idea columns to existing local `macro-regime.db` files; PostgreSQL uses EF Core migrations.
- Data Imports admin page for manually refreshing active FRED `ExternalSeries` mappings and reviewing persisted observations.

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

The startup path applies EF Core migrations for Postgres or creates/upgrades the local SQLite schema before seeding the sample monitor data. The legacy lightweight column upgrade remains SQLite-only and is not run against Postgres.


## Startup synchronization safety

Startup synchronization is designed to be safe to run repeatedly against the same SQLite or Supabase/Postgres database.

- Schema migrations and lightweight startup schema upgrades are applied idempotently before seeding.
- The data source registry is additive only: startup inserts missing official/public source definitions by `Name` and does not update, disable, delete, or overwrite existing `DataSources` rows.
- Initial sample macro seed data is inserted only when the database has no `MacroFactors` rows. If any macro factors already exist, startup skips sample `MacroFactors`, `Indicators`, `IndicatorObservations`, `FactorScores`, `WeeklyReviews`, and `TradeIdeas`.
- The initial FRED external series mappings are additive only: startup inserts missing approved mappings by data source, series id, and indicator id, and does not update, disable, delete, or overwrite existing `ExternalSeries` rows.
- Startup never overwrites imported observations, edited factor definitions, user-created journal entries, or custom/disabled source rows.
- Each startup writes one `StartupSyncRuns` audit row with the applied migration list, seeding counts, completion status, message, and any error message captured before the failure is rethrown.

## Historical data ingestion plan

Historical macro data ingestion is intended to be source-driven rather than copy/pasted by hand. The database foundation added for future import work separates provider metadata, app-to-provider series mappings, audited import attempts, and normalized observations.

- `DataSources` defines official/public providers such as FRED, BLS, EIA, and Treasury Fiscal Data, including provider type, base URL, whether an API key is expected, and operational notes. No API keys or secrets are stored in source control.
- `ExternalSeries` will map one app `Indicator` to one provider series or endpoint, including the provider series id, endpoint, frequency, units, transform, and response fields used for observation dates and values.
- `DataImportRuns` will audit each import attempt with its source, start/finish timestamps, status, row counts, errors, and notes.
- `IndicatorObservations` stores normalized observations used by the app and now includes nullable source/import tracking fields for external series, import runs, source names, release dates, vintage dates, and timestamps.

v0.7.3 persists fetched FRED observations into `IndicatorObservations` for existing FRED `ExternalSeries` mappings only. BLS, EIA, and Treasury clients remain placeholders, no API keys or secrets are committed, and dashboard scoring remains unchanged until the separate manual scoring action is run.

## Initial FRED series mappings

v0.7.0 seeds only the first reviewed FRED `ExternalSeries` mappings for current macro indicators. v0.7.3 can fetch observations for these mappings through the import service when a FRED API key is configured and persist them to `IndicatorObservations`, but imports still do not automatically recalculate scoring or change dashboard behavior.

| Indicator | FRED series | Endpoint | Frequency | Units | Transform |
| --- | --- | --- | --- | --- | --- |
| Core CPI Trend | `CPILFESL` | `/series/observations` | Monthly | PercentChangeFromYearAgo | `pc1` |
| Market Complacency / VIX Index | `VIXCLS` | `/series/observations` | Daily | Level | `lin` |
| Fiscal/Treasury Stress / 10Y Treasury proxy | `DGS10` | `/series/observations` | Daily | Level | `lin` |

Other indicators intentionally remain unmapped until their source and series choices are reviewed. In particular, this version does not add FRED mappings for Trimmed Mean CPI, WTI Crude Oil, or ISM Manufacturing PMI.

## FRED API configuration

v0.7.3 can fetch FRED observations through the import service for the existing `CPILFESL`, `VIXCLS`, and `DGS10` mappings. The fetched rows are counted in `DataImportRuns` and are safely upserted into `IndicatorObservations` by `IndicatorId` plus `ObservationDate`. Existing observations are skipped by default and are overwritten only when `ForceRefresh` is explicitly enabled.

Store the FRED API key outside source control. For local development, use .NET user secrets from the repository root:

```powershell
dotnet user-secrets set "Fred:ApiKey" "<your-fred-api-key>" --project .\src\MacroRegimeFactorMonitor\MacroRegimeFactorMonitor.csproj
```

The default FRED base URL is `https://api.stlouisfed.org/fred`. You normally do not need to override it, but if needed you can set `Fred:BaseUrl` through user secrets or environment variables. No real FRED API key should be committed to `appsettings.json` or any other tracked file.

FRED requires the API key to be sent as the `api_key` query parameter on API requests. The app suppresses `System.Net.Http.HttpClient` informational request/response logging so framework logs do not write full FRED request URLs, including query-string secrets, while application warnings and errors remain enabled.

## Manual import testing

v0.7.3 updates the controlled **Data Imports** admin page at `/imports` for manually testing existing `ExternalSeries` mappings through the app. The page lists active mappings, provides optional from/to date filters, and can trigger the import service for one mapping at a time.

FRED imports can be tested from this page when `Fred:ApiKey` is configured outside source control. Each attempt writes a `DataImportRun` with timestamps, status, row counts, notes, and any error message so import behavior can be reviewed without changing scoring.

v0.7.3 persists fetched observations into `IndicatorObservations`. Existing observations with the same `IndicatorId` and `ObservationDate` are skipped by default, so seeded/sample observations are not overwritten during normal manual imports. `ForceRefresh` controls overwrites when exposed or used, and it defaults to false in the admin UI. The dashboard continues to use the current persisted `FactorScores` until the separate manual scoring action is run; imported observations are not converted into `FactorScores` automatically.

## Operational workflow

v0.8.3 adds an **Operational Workflow** page at `/workflow` that coordinates the manual macro-monitor process in one place: refresh active FRED data, review import freshness, recalculate ImportedManual scores, inspect confidence/data-quality diagnostics, and then open the dashboard for regime interpretation. The existing `/imports`, `/scoring`, and dashboard pages remain available and continue to work independently.

The **Run full manual workflow** control is still an explicit manual button, not a scheduled job. It sequentially refreshes active FRED series, recalculates ImportedManual scores for the selected ScoreDate, and reloads the workflow status summary. If one or more series fails during refresh, scoring still runs and the page warns the operator to review freshness and scoring confidence before using the dashboard.

Refresh and scoring remain auditable through the existing data model: import activity is recorded in `DataImportRuns`, and manual score outputs are stored in `FactorScores` with the ImportedManual data mode and scoring model version. The workflow does not delete Sample scores or older ImportedManual score versions/dates.

There is no automatic trading, broker integration, execution routing, TradeIdea automation, hidden background job, automatic startup refresh, automatic startup scoring, scheduled job, or new external data source in this workflow. Azure deployment will later use the same manual workflow first before any scheduled operational model is introduced.

## Operational manual refresh

v0.7.4 keeps all import activity manually controlled from the **Data Imports** admin page at `/imports`. Operators can refresh one active `ExternalSeries` with explicit from/to dates or click **Refresh all active FRED series** to sequentially refresh every active FRED mapping (`CPILFESL`, `VIXCLS`, and `DGS10` when seeded and active). No scheduled Quartz job is registered, no import runs automatically on app startup, and no API keys or secrets are displayed or stored in source control.

The refresh-all action calculates overlap windows by frequency before each per-series import: daily series re-fetch from the latest persisted observation date minus 7 days, monthly series re-fetch from the latest persisted observation date minus 6 months, unknown frequencies re-fetch from the latest persisted observation date minus 30 days, and series with no persisted observations default to the last 2 years. Refresh-all always uses `ForceRefresh = true` so overlap windows can capture revised FRED values. Each series is attempted independently; a failed series is reported but does not stop later series.

The `/imports` page now shows informational freshness badges for each external series based on the latest persisted `IndicatorObservations` row: daily series are fresh within 5 calendar days, monthly series within 45 calendar days, unknown frequencies within 14 calendar days, and series without observations are marked missing. These badges are display-only and do not block imports. The page also shows a read-only recent observation history table so operators can inspect persisted observations and their `DataImportRun` links.

Dashboard and scoring behavior remain unchanged in v0.7.4. Imported observations are not automatically converted into `FactorScores`, there is no automatic `FactorScore` recalculation, and Quartz scheduled refresh remains intentionally deferred.

## Import architecture

v0.6.3 adds the import architecture skeleton only. The application now has DTOs, source-client interfaces, a source-client factory, placeholder clients, and an observation import service structure that future PRs can extend without changing dashboard, scoring, journal, or trading behavior.

- The FRED client can call `/series/observations` for existing FRED mappings when `Fred:ApiKey` is configured; BLS, EIA, and Treasury Fiscal Data clients intentionally remain placeholders.
- No API keys or secrets are introduced.
- Import attempts are audited through `DataImportRuns`, with start/finish timestamps, status, row counts, notes, and errors.
- Fetched observations are normalized into `IndicatorObservations` while preserving existing data by default and linking imported rows back to their `DataImportRun`.
- This version can fetch FRED rows for review and insert or force-refresh observations, but it does not recalculate or update dashboard `FactorScores`.

## Scoring approach

For each seeded factor, the app computes a normalized raw score from its indicator observation:

```text
raw score = ((value - baseline) / volatility) * direction
weighted score = raw score * factor weight
```

`direction` is `1` when higher values are constructive for risk assets and `-1` when higher values are a macro risk. Weighted scores are summed into category scores and a composite dashboard score.

A measurable factor score is therefore the data-backed layer of the dashboard: one observed indicator is compared with its baseline and volatility, direction-adjusted, and weighted by the factor's configured importance. The factor table also shows a pressure contribution so the user can see whether each scored factor is adding macro pressure, providing relief, or remaining balanced.

Dashboard factor impact wording uses pressure-aware labels: `Pressure rising`, `Mild pressure`, `Relief`, `Balanced`, and the Market Complacency-specific `Complacency pressure`.


## Scoring diagnostics

v0.8.2 keeps the manual `ImportedManual` scoring workflow but makes it less toy-like by recording both point-in-time diagnostics and simple observation-window context. Each recalculated `FactorScore` can record the source observation date/value, previous observation date/value, absolute and percent one-step change, days since the source observation, data quality status, window counts/dates/min/max/average/first/last values, window change and percent change, simple window slope, approximate window acceleration, scoring confidence, and scoring confidence notes. Existing `Sample` scores remain valid; their diagnostic fields may be null.

Freshness is informational only and does not block scoring, imports, dashboard display, or manual review:

| Series frequency | Fresh when latest observation is | Stale when latest observation is |
| --- | ---: | ---: |
| Daily | `<= 5` calendar days old | `> 5` calendar days old |
| Monthly | `<= 45` calendar days old | `> 45` calendar days old |
| Unknown | `<= 14` calendar days old | `> 14` calendar days old |

Diagnostic statuses are:

- `Fresh` or `Stale` for mapped factors with a usable imported observation on or before `ScoreDate`.
- `Missing` for mapped factors when no imported observation exists on or before `ScoreDate`; the manual raw score remains neutral (`0`).
- `Placeholder` for unmapped factors such as Inflation Breadth, Energy Shock, and Growth Stress until an imported observation mapping is added.

Scoring confidence is informational and based on data freshness plus lookback-window sample size:

- `High` for fresh mapped data with at least 8 monthly observations or 30 daily observations in the lookback window.
- `Medium` for fresh or stale mapped data with at least 3 observations in the lookback window.
- `Low` for mapped data with thinner lookback context.
- `Missing` when mapped data has no imported observation on or before `ScoreDate`.
- `Placeholder` when no imported observation mapping exists yet.

Diagnostics are populated only when a human runs the manual recalculation from `/scoring`. They do not run automatically on startup, on a schedule, or after imports, and they do not add broker integration, automatic trading, `TradeIdea` automation, or new external data sources. The scoring model remains intentionally placeholder/simple; freshness, slope, acceleration, and confidence context are transparency aids, not a production macro model.

## Window-based ImportedManual scoring

v0.8.2 writes `DataMode = ImportedManual` scores with `ScoringModelVersion = imported-manual-v0.8.2`. It still uses placeholder/simple rules, but the mapped factors now use observation windows as diagnostics and as first-pass scoring adjustments:

- **Inflation Pressure / `CPILFESL`** uses the latest core CPI YoY value for the base score and the 12-month window change for a small adjustment. A 12-month increase of at least `+0.75` adds `+0.5`; a decrease of at least `-0.75` subtracts `0.5`; final raw scores are clamped to `[-2, +2]`.
- **Fiscal/Treasury Stress / `DGS10`** uses the latest 10-year Treasury yield level for the base score and the 60-calendar-day yield change for a small adjustment. A 60-day increase of at least `+0.50` adds `+0.5`; a decrease of at least `-0.50` subtracts `0.5`; final raw scores are clamped to `[-2, +2]`. `DGS10` remains a rough Treasury-rate proxy, not true fiscal stress or term premium.
- **Market Complacency / `VIXCLS`** uses the latest VIX level for the base score and the 60-calendar-day VIX trend for a small adjustment. A VIX drop of at least `-5.0` adds `+0.5` because sharply falling volatility can increase complacency pressure; a VIX rise of at least `+5.0` subtracts `0.5` because visible fear reduces complacency pressure; final raw scores are clamped to `[-2, +2]`. Low VIX increases Market Complacency pressure; high VIX reduces complacency pressure.
- Unknown-frequency mappings, if added later, use a 90-calendar-day lookback window by default.

Window fields are part of the persisted score row so the `/scoring` table and dashboard can explain what changed. Confidence is also persisted, but it is informational; it does not suppress a score or trigger any automation. Manual recalculation for the same `ScoreDate`, `DataMode`, factor, and `ScoringModelVersion` updates the existing six rows, while the v0.8.2 version key leaves older v0.8.0/v0.8.1 rows intact. The dashboard prefers the latest `ImportedManual` model version for a selected/latest date when multiple imported-manual versions exist.

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

## Manual imported-data scoring

v0.8.0 added the first manual bridge from imported FRED observations to `FactorScores`; v0.8.2 keeps that bridge manual-only and writes the current version as `ScoringModelVersion = imported-manual-v0.8.2`. After importing observations, use the **Scoring** admin page to run **Recalculate current scores from imported observations** for a selected `ScoreDate` (default: today in UTC). The recalculation uses the latest imported observation on or before that score date plus the configured lookback window for mapped factors.

Important caveats:

- This is deliberately simple placeholder scoring, not production-quality macro scoring or investment advice.
- Only existing imported FRED mappings are used: `CPILFESL` for Inflation Pressure, `DGS10` for Fiscal/Treasury Stress, and `VIXCLS` for Market Complacency.
- `DGS10` is only a rough Treasury-rate proxy; it is not true fiscal stress or a true term-premium model.
- Inflation Breadth, Energy Shock, and Growth Stress are not mapped yet, so manual scoring creates explicit neutral placeholders with `SourceObservationCount = 0`.
- Scoring does not run automatically on startup, on a schedule, or after imports. Imports never automatically recalculate scores.
- No broker integration, automatic trading, `TradeIdea` automation, or hidden background scoring jobs are included.
- The dashboard labels whether it is displaying `Sample`, `ImportedManual`, or mixed data modes and prefers `ImportedManual` scores when they exist for the latest displayed score date. When multiple imported-manual model versions exist for that date, it prefers `imported-manual-v0.8.2` over older imported-manual rows.
- Scheduled jobs remain deferred.
