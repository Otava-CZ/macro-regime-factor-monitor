# Temporary preview access

The app can optionally protect temporary shared preview deployments with a single access token.

This is intended for short-lived preview links such as GitHub Codespaces forwarded ports or local tunnel tests. It is not a replacement for production authentication.

## Configuration

Set these environment variables only for a temporary preview instance:

```text
TemporaryAccess__Enabled=true
TemporaryAccess__Token=<long-random-temporary-token>
```

When disabled or when no token is configured, the app behaves normally.

When enabled, requests without a valid token receive `401 Access token required`.

Open the app once with:

```text
https://<preview-host>/?access_token=<long-random-temporary-token>
```

The app sets an HttpOnly cookie and redirects back to the same URL without the token query string.

## Safety rules

- Use a long random token.
- Do not reuse a real password.
- Do not commit the token.
- Do not leave the preview running longer than needed.
- Stop the tunnel/codespace after testing.
- Do not treat this as production-grade authentication.

## Docker example

```powershell
docker run --rm -p 18080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ASPNETCORE_URLS=http://+:8080 `
  -e StartupSync__FailFast=false `
  -e TemporaryAccess__Enabled=true `
  -e TemporaryAccess__Token="<long-random-temporary-token>" `
  macro-regime-monitor:local
```
