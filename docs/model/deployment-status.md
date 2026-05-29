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

## Current code state

- Repository: `Otava-CZ/macro-regime-factor-monitor`
- Default branch: `main`
- Dockerfile exists in repo root.
- Temporary access token gate was removed from `main`.

Relevant removal commits:
- `ed4e89877a841ccadeb52aa72582fbae2c1ad459` — Remove temporary access token gate.
- `ec51af87c9e4716630bdc8c50ee29112fa6ba587` — Remove temporary preview access documentation.

## Render configuration

Recommended service settings:
- Runtime/Language: Docker
- Dockerfile path: `./Dockerfile`
- Branch: `main`
- Health check path: `/ping`
- Instance: Free

Recommended environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
PORT=8080
StartupSync__FailFast=false
```

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

User reported Claude.ai could access the Render page. Prior ChatGPT instance could not fetch it, returning `Cache miss`.

## MonsterASP.NET note

User asked about MonsterASP.NET deployment.

No reliable public docs were available in prior ChatGPT environment. Treat as generic Windows ASP.NET Core hosting until the control panel is visible.

Likely deployment method:
- `dotnet publish`
- upload published output via FTP/File Manager/Web Deploy
- ensure .NET 8 / ASP.NET Core runtime support
- set environment variables/app settings for Supabase/FRED if supported.
