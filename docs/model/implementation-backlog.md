# Implementation Backlog

Created: 2026-05-29

## Current priority order

### 1. Stabilize real deployment and branch workflow

**Goal:** Keep Render deployment working without temporary token gate and make implementation promotion predictable.

Tasks:
- Confirm latest promoted `main` is deployed to Render.
- Keep `develop` synchronized with promoted `main` before new implementation chunks.
- Direct new implementation work to feature branches targeting `develop`.
- Promote `develop` to `main` only after review, deployment, and user validation.
- Confirm `/`, `/ping`, `/health`, `/ready`, `/system`, `/workflow`, `/imports`, `/scoring`, `/api/model/snapshot`.
- Remove obsolete Render env vars: `TemporaryAccess__Enabled`, `TemporaryAccess__Token`.
- Configure production-like variables when ready:
  - keep SQLite for the current Render Free early prototype if it is working,
  - `Fred__ApiKey=<FRED API key>`
  - `Fred__BaseUrl=https://api.stlouisfed.org/fred`
  - `StartupSync__FailFast=false`
  - defer `Database__Provider=Postgres` and `ConnectionStrings__MacroRegime=<Supabase session-pooler connection string>` until the durability upgrade.

Acceptance:
- App opens without `access_token`.
- `/ping` returns running.
- `/health`, `/ready`, and `/api/model/snapshot` return safe diagnostics.
- Snapshot includes safe deployment metadata when configured or exposed by host environment.
- Snapshot includes production-data readiness blockers and next actions without exposing secrets.
- No secrets are shown in UI/logs/snapshot/docs.
- `develop` is the target branch for new implementation PRs.

---

### 2. Assistant-readable model snapshot

**Goal:** Let an assistant review model output without needing to browse the Blazor UI.

Endpoint:

```text
/api/model/snapshot
```

Snapshot should include:
- as-of timestamp,
- safe deployment metadata,
- data provider/database status,
- top-level `operationalState` with data mode, production readiness, blocking reasons, and next actions,
- FRED configured status and active FRED series count,
- factor scores,
- factor slopes/changes,
- data freshness warnings,
- derived interpretations,
- latest import status, latest successful import, and latest import by data source,
- latest scoring status and latest score dates by data mode,
- selected score mode used by the dashboard/snapshot,
- current trade candidates/watchlist items,
- warnings/open questions.

Acceptance:
- No secrets exposed.
- Output is deterministic and readable.
- Can be copied, downloaded, or posted to GitHub issue/artifact.
- Clearly distinguishes sample mode from imported/manual scoring mode.

Current status:
- Initial endpoint is deployed on `main` and works on Render.
- Follow-up diagnostics chunk adds safe deployment metadata and develop-first workflow documentation.
- Production-data readiness diagnostics are implemented on the feature branch for review.

---

### 3. Production-like data foundation

**Goal:** Move the deployed app from sample-only mode toward real import/scoring readiness.

Tasks:
- Configure or verify production-like Render/FRED variables outside source control:
  - `Fred__ApiKey=<FRED API key>`
  - `Fred__BaseUrl=https://api.stlouisfed.org/fred`
  - `StartupSync__FailFast=false`
- Keep SQLite acceptable for the current Render Free early prototype when it is working; treat Supabase/Postgres as a future durability upgrade, not as a prerequisite for the import/manual-scoring loop.
- Ensure `/ready`, `/system`, and `/api/model/snapshot` explain precisely why the app is or is not data-ready.
- Add clearer latest-import and latest-scoring diagnostics by source/mode if needed.
- Keep sample data as fallback only.
- Keep secrets in Render only; do not commit them to source, docs, examples, logs, UI output, or snapshot JSON.

Acceptance:
- Snapshot clearly says whether the dashboard is using `Sample` or `ImportedManual` data.
- `operationalState.productionDataReady` is false whenever `DataMode` is `Sample`.
- Missing FRED/import/scoring prerequisites are visible as blocking reasons, while SQLite in Production is visible as a non-blocking current storage-mode limitation.
- No secrets are exposed.

---

### 4. Real data import and manual scoring loop

**Goal:** Make the operational loop work end-to-end.

```text
Fetch observations -> persist observations -> run manual scoring -> dashboard/snapshot uses ImportedManual
```

Tasks:
- Confirm existing approved FRED mappings.
- Harden manual import action.
- Harden manual scoring action from imported observations.
- Store `ImportedManual` scores with explicit scoring model version.
- Prefer `ImportedManual` over `Sample` when available.
- Show source observation dates and data quality status in dashboard/snapshot.
- Keep SQLite as a non-blocking warning for the Render Free prototype.

Manual prototype flow:
1. Configure `Fred__ApiKey` in Render environment variables.
2. Open `/imports`.
3. Run manual FRED imports for active configured series.
4. Open `/scoring`.
5. Run manual scoring to create `ImportedManual` factor scores.
6. Verify `/api/model/snapshot` and the dashboard select `ImportedManual`.

Acceptance:
- After manual workflow, snapshot shows `dataMode = ImportedManual`.
- Latest import status and latest scoring status are populated.
- Factor score source observation fields are populated where data exists.
- No scheduled automatic scoring.

---

### 5. Data freshness and slope/acceleration

**Goal:** Make current-data quality explicit.

Tasks:
- Track data age per indicator.
- Show stale-data warnings.
- Add factor slope/acceleration where enough historical data exists.
- Penalize stale inputs in interpretation confidence.

---

### 6. Trade candidate review structure

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

### 7. Official Backstop Events

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

### 8. Market Complacency expansion

**Goal:** Expand Market Complacency beyond static sample values.

Potential sub-signals:
- valuation stretch,
- volatility complacency,
- credit-spread complacency,
- speculative excess/options activity,
- concentration risk,
- AI/mega-cap enthusiasm proxy if data supports it.

---

### 9. Weekly hypothesis review workflow

**Goal:** Make weekly review operational.

Tasks:
- Show what changed since previous review.
- Show confirmed/invalidated hypotheses.
- Show open questions.
- Show approved/rejected/needs-evidence trade candidates.
- Allow post-mortem notes.
