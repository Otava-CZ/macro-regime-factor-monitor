# Model Decision Log

Created: 2026-05-29

## Decision 001 — Manual-only scope

**Decision:** The project is a manual macro-monitoring and journal system only.

**Rationale:** The user wants decision support, not automated trading.

**Implementation impact:**
- No broker integration.
- No automatic trading.
- No execution routing.
- Trade candidates remain review/watchlist items.

---

## Decision 002 — Six base factors

**Decision:** Use six base macro factors:
1. Inflation Pressure
2. Inflation Breadth
3. Energy Shock
4. Growth Stress
5. Fiscal/Treasury Stress
6. Market Complacency

**Rationale:** These factors cover inflation, supply shocks, growth, fiscal/liquidity risk, and market pricing/complacency.

**Implementation impact:**
- Dashboard and scoring should preserve these six factors as the base layer.
- Derived interpretations may be added above them.

---

## Decision 003 — Derived regime interpretations

**Decision:** Add derived interpretations above raw factors:
- inflation/stagflation pressure,
- fiscal/Treasury stress,
- hard-landing pressure,
- market complacency/mispricing,
- liquidity/backstop stress.

**Rationale:** Raw factor scores alone are not enough for weekly decision review.

**Implementation impact:**
- Model snapshot/report should expose both raw factors and derived interpretations.

---

## Decision 004 — Buffett/valuation/speculation discussion

**Decision:** Stretched valuation and speculative excess should raise Market Complacency risk, but should not be used as a precise crash-timing signal.

**Rationale:** Buffett’s comments emphasize patience, valuation discipline, and inability to predict crash timing.

**Implementation impact:**
- Add/strengthen Market Complacency sub-signals: valuation stretch, volatility complacency, credit complacency, speculative excess/options activity when available.
- Trade candidates should require explicit triggers and invalidation.

---

## Decision 005 — Official Backstop Events

**Decision:** Add Official Backstop Events as a monitored concept under Fiscal/Treasury Stress and Liquidity/Backstop interpretation.

**Rationale:** Official support may reduce short-term liquidity risk but indicate rising structural fragility.

**Implementation impact:**
- Add backlog item for an OfficialBackstopEvent domain model/table or equivalent.
- Model output should separate liquidity relief from structural stress.

---

## Decision 006 — Real/current data matters more than static sample tuning

**Decision:** Prioritize current/regular data imports, freshness, slopes, and observable model reports over continued tuning of static seed/sample data.

**Rationale:** Static data is insufficient for an operational macro monitor.

**Implementation impact:**
- Improve imports/scoring freshness.
- Expose latest observations and data age.
- Add model snapshot/report for review.

---

## Decision 007 — Deployment target

**Decision:** Render Free is the current real deployment target. Azure F1 is abandoned for now.

**Rationale:** Azure F1 hit quota/site disabled. Render deployment is publicly reachable from normal clients, although the overloaded ChatGPT instance could not fetch it.

**Implementation impact:**
- Keep Dockerfile deployment path.
- Use Render environment variables for secrets.
- Do not reintroduce temporary token gate unless explicitly requested.
