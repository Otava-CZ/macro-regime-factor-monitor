# Deployment Status

Created: 2026-05-29

## Current deployment target

Current target:

```text
Render Free Web Service
```

Current public URL:

```text
https://macro-regime-factor-monitor.onrender.com/
```

Assistant-readable live model snapshot:

```text
https://macro-regime-factor-monitor.onrender.com/api/model/snapshot
```

## Branch and promotion workflow

Target workflow from now on:

```text
feature branch / Codex implementation
-> PR into develop
-> review and deploy/test develop-compatible build
-> promote develop to main only after validation
```

Rules:
- New implementation work should target `develop`, not `main`.
- `main` is the promoted/stable branch.
- `develop` should stay synchronized with promoted `main` before starting new chunks.
- Strategy/model changes must first be captured in model docs or decision log before implementation.
- No broker integration, automatic trading, execution routing, or order management may be introduced.

Current synchronization note:
- PR `#63` synchronized `develop` with the promoted `main` state after the snapshot endpoint was deployed.

## Current code state

- Repository: `Otava-CZ/macro-regime-factor-monitor`
- Default branch: `main`
- Development branch: `develop`
- Dockerfile exists in repo root.
- Temporary access token gate was removed from `main`.
- `/api/model/snapshot` is deployed and returns safe model/deployment diagnostics.
- `/api/model/snapshot` now includes an `operationalState` section with production-data readiness, blocking reasons, and next actions while keeping existing snapshot fields backward-compatible.

Relevant removal commits:
- `ed4e89877a841ccadeb52aa72582fbae2c1ad459` — Remove temporary access token gate.
- `ec51af87c9e4716630bdc8c50ee29112fa6ba587` — Remove temporary preview access documentation.

## Render configuration

Recommended service settings:
- Runtime/Language: Docker
- Dockerfile path: `./Dockerfile`
- Branch: `main` for stable promoted deployment, or `develop` only if a separate preview service is created.
- Health check path: `/ping`
- Instance: Free

Recommended base environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
PORT=8080
StartupSync__FailFast=false
```

Optional safe deployment metadata variables for `/api/model/snapshot`:

```text
Render__GitBranch=<branch name>
Render__GitCommit=<commit sha>
Render__ServiceName=<service name>
Render__DeployId=<deploy id>
```

These values are not secrets. If Render exposes equivalent `RENDER_*` or `GIT_*` variables, the app will read those automatically as well.

Required Render variables for the current SQLite-backed imported/manual operation:

```text
Fred__ApiKey=<FRED API key>
Fred__BaseUrl=https://api.stlouisfed.org/fred
StartupSync__FailFast=false
```

SQLite is acceptable for the current Render Free early prototype. `Database__Provider=Postgres` and `ConnectionStrings__MacroRegime=<Supabase session-pooler connection string>` are future durability-upgrade settings, not prerequisites for validating the import/manual-scoring loop. Secrets such as the Supabase session-pooler connection string and FRED API key must be configured only in Render environment variables. Do not commit secrets to source, docs, examples, logs, UI output, or snapshot JSON.

Obsolete variables after token removal:

```text
TemporaryAccess__Enabled
TemporaryAccess__Token
```

Remove these from Render if present.

## Deployment history

### Azure App Service F1

Status: abandoned.

Reason:
- GitHub Actions deploy eventually succeeded.
- Runtime failed due to Azure Free F1 quota: `QuotaExceeded`, `Site Disabled`.
- Logs/SCM/SSH were not useful while site was disabled.

Do not return to Azure F1 unless the user explicitly chooses a paid tier.

### Local Docker

Status: working.

Local build:

```powershell
docker build -t macro-regime-monitor:local .
```

Local run:

```powershell
docker run --rm -p 18080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ASPNETCORE_URLS=http://+:8080 `
  -e StartupSync__FailFast=false `
  macro-regime-monitor:local
```

Local `/ping` worked.

### ngrok / Cloudflare / Codespaces

Status: worked for user, not accessible from the overloaded ChatGPT instance.

### Render

Status: current real host.

Current confirmed state from the Render Free SQLite prototype before this manual import/scoring hardening chunk:
- Environment: Production
- Database provider: SQLite
- Database reachable: true
- FRED API key configured: true
- FRED ready: true
- Active FRED series count: 3
- Data mode: Sample
- Latest import status: null
- Latest successful import status: null
- Latest ImportedManual score date: null
- Production data ready: false

Next Render configuration step for production-like data:
- keep the current Render Free SQLite storage mode for the early prototype if it is working,
- set `Fred__ApiKey=<FRED API key>`,
- set `Fred__BaseUrl=https://api.stlouisfed.org/fred`,
- keep `StartupSync__FailFast=false`,
- run manual import/scoring workflow,
- verify snapshot `operationalState.productionDataReady` is `true` and reports `ImportedManual` instead of `Sample`.

SQLite in Production is a known current storage-mode limitation, not a hard blocker for the Render Free import/manual-scoring loop. Supabase/Postgres remains the future durability upgrade, but Postgres is not required before validating imports and manual scoring.

The snapshot and System page should represent missing FRED/import/scoring prerequisites as blocking reasons or warnings, not exceptions, and must never display the actual API key or database connection string.

## MonsterASP.NET note

User asked about MonsterASP.NET deployment.

No reliable public docs were available in prior ChatGPT environment. Treat as generic Windows ASP.NET Core hosting until the control panel is visible.

Likely deployment method:
- `dotnet publish`
- upload published output via FTP/File Manager/Web Deploy
- ensure .NET 8 / ASP.NET Core runtime support
- set environment variables/app settings for Supabase/FRED if supported.

## Manual import and manual scoring prototype flow

Current Render Free prototype target after the manual import/scoring hardening chunk:

1. Configure `Fred__ApiKey` in Render environment variables. Do not place the key in source, logs, docs, UI text, or JSON output.
2. Open `/imports`.
3. Run the manual FRED import for active configured series, or use the manual "Refresh all active FRED series" action.
4. Confirm the imports page shows safe import status, source, external series id, last successful import time, rows read, rows inserted, rows updated, and any safe error summaries.
5. Open `/scoring`.
6. Run the manual ImportedManual scoring action for the desired `ScoreDate`.
7. Verify `/dashboard`, `/system`, and `/api/model/snapshot` show selected `DataMode = ImportedManual` once ImportedManual scores exist.
8. Verify `/api/model/snapshot` has latest import status, latest successful import status, latest imports by data source, and `latestImportedManualScoreDate` populated.

SQLite remains acceptable for the current Render Free prototype and should appear only as a non-blocking storage-mode limitation. `operationalState.productionDataReady` should become `true` only when the database is reachable, FRED is configured, at least one successful import exists, ImportedManual scores exist, and the selected dashboard/snapshot mode is `ImportedManual`.
