# Implementation Backlog

Created: 2026-05-29

## Current priority order

### 1. Stabilize real deployment

**Goal:** Keep Render deployment working without temporary token gate.

Tasks:
- Confirm latest `main` is deployed to Render.
- Confirm `/`, `/ping`, `/health`, `/ready`, `/system`, `/workflow`, `/imports`, `/scoring`.
- Remove obsolete Render env vars: `TemporaryAccess__Enabled`, `TemporaryAccess__Token`.
- Configure production-like variables when ready:
  - `Database__Provider=Postgres`
  - `ConnectionStrings__MacroRegime=<Supabase session-pooler connection string>`
  - `Fred__ApiKey=<FRED API key>`
  - `Fred__BaseUrl=https://api.stlouisfed.org/fred`
  - `StartupSync__FailFast=false`

Acceptance:
- App opens without `access_token`.
- `/ping` returns running.
- `/health` and `/ready` return safe diagnostics.
- No secrets are shown in UI/logs.

---

### 2. Assistant-readable model snapshot

**Goal:** Let an assistant review model output without needing to browse the Blazor UI.

Add endpoint or report generator:

```text
/api/model/snapshot
```

or internal report export producing JSON/Markdown.

Snapshot should include:
- as-of timestamp,
- data provider/database status,
- factor scores,
- factor slopes/changes,
- data freshness warnings,
- derived interpretations,
- latest import status,
- latest scoring status,
- current trade candidates/watchlist items,
- warnings/open questions.

Acceptance:
- No secrets exposed.
- Output is deterministic and readable.
- Can be copied, downloaded, or posted to GitHub issue/artifact.

---

### 3. Trade candidate review structure

**Goal:** Convert trade candidates into structured reviewable objects.

Fields:
- id,
- thesis,
- candidate type,
- asset class/category,
- direction,
- supporting factors,
- conflicting evidence,
- trigger,
- invalidation,
- catalyst,
- risk notes,
- max-loss concept,
- time horizon,
- approval status.

Allowed statuses:
- Pending review.
- Approved for watchlist.
- Rejected.
- Needs more evidence.
- Needs model correction.

---

### 4. Data freshness and slope/acceleration

**Goal:** Make current-data quality explicit.

Tasks:
- Track data age per indicator.
- Show stale-data warnings.
- Add factor slope/acceleration where enough historical data exists.
- Penalize stale inputs in interpretation confidence.

---

### 5. Official Backstop Events

**Goal:** Implement official liquidity/backstop monitoring.

Possible entity:

```text
OfficialBackstopEvent
```

Fields:
- Id
- EventDate
- Source
- EventType
- Description
- Jurisdiction
- EstimatedScaleUsd
- LiquidityReliefScore
- StructuralStressScore
- Confidence
- Notes
- CreatedAtUtc
- UpdatedAtUtc

---

### 6. Market Complacency expansion

**Goal:** Expand Market Complacency beyond static sample values.

Potential sub-signals:
- valuation stretch,
- volatility complacency,
- credit-spread complacency,
- speculative excess/options activity,
- concentration risk,
- AI/mega-cap enthusiasm proxy if data supports it.

---

### 7. Weekly hypothesis review workflow

**Goal:** Make weekly review operational.

Tasks:
- Show what changed since previous review.
- Show confirmed/invalidated hypotheses.
- Show open questions.
- Show approved/rejected/needs-evidence trade candidates.
- Allow post-mortem notes.
