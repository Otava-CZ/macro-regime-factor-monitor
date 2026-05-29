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

Recommended environment variables:

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

When production-like data is enabled:

```text
Database__Provider=Postgres
ConnectionStrings__MacroRegime=<Supabase session-pooler connection string>
Fred__ApiKey=<FRED API key>
Fred__BaseUrl=https://api.stlouisfed.org/fred
```

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

Current confirmed state from live `/api/model/snapshot` after PR `#62`:
- Environment: Production
- Database provider: SQLite
- Database reachable: true
- FRED API key configured: false
- Data mode: Sample
- Latest score date: 2026-04-30
- Composite regime: Defensive Slowdown

Next Render configuration step for production-like data:
- move to Supabase/Postgres connection string,
- configure FRED API key,
- run manual import/scoring workflow,
- verify snapshot reports `ImportedManual` instead of `Sample`.

## MonsterASP.NET note

User asked about MonsterASP.NET deployment.

No reliable public docs were available in prior ChatGPT environment. Treat as generic Windows ASP.NET Core hosting until the control panel is visible.

Likely deployment method:
- `dotnet publish`
- upload published output via FTP/File Manager/Web Deploy
- ensure .NET 8 / ASP.NET Core runtime support
- set environment variables/app settings for Supabase/FRED if supported.
