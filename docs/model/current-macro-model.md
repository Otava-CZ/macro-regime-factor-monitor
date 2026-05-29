# Current Macro Model

Created: 2026-05-29

## Model purpose

The model is a manual macro-regime monitoring framework. It is not a fixed classifier and it does not produce automatic trades.

Core pipeline:

```text
Raw data
-> normalized macro observations
-> factor scores
-> slope/acceleration
-> regime interpretation
-> market mispricing/complacency check
-> trade candidates for manual review
-> journal and weekly hypothesis review
```

## Base factors

The approved base scoring layer contains six factors:

1. **Inflation Pressure** — headline/core inflation pressure and inflation momentum.
2. **Inflation Breadth** — whether inflation pressure is narrow or broad.
3. **Energy Shock** — oil/gas/energy-price stress and supply-shock risk.
4. **Growth Stress** — recession/hard-landing pressure.
5. **Fiscal/Treasury Stress** — fiscal sustainability, Treasury-market stress, funding/liquidity pressure, and official support needs.
6. **Market Complacency** — whether markets are underpricing macro/fiscal/liquidity risk.

## Derived interpretations

Derived interpretations sit above base factor scores:

- inflation/stagflation pressure,
- fiscal/Treasury stress,
- hard-landing pressure,
- market complacency/mispricing,
- liquidity/backstop stress.

## Buffett / speculative excess model note

The user reviewed a Warren Buffett-related transcript about Berkshire’s cash pile, stretched market valuations, AI enthusiasm, short-term options gambling, and crash unpredictability.

Approved model implications:

- High valuations and speculative behavior should raise Market Complacency risk.
- This should not be treated as a precise crash-timing signal.
- The model should favor patience, dry-powder/watchlist readiness, and evidence-based trade review over constant trading.
- Short-term options/0DTE activity, if available, should be considered a speculative-excess proxy.
- Valuation stretch should influence forward-return and risk/reward interpretation, not trigger automatic shorts.

## Treasury/liquidity backstop model note

The user raised concern about possible US emergency swap/liquidity/backstop mechanisms with Gulf states or other foreign official holders, potentially preventing forced Treasury selling and acting as an indirect bailout.

Approved model implications:

- Add an **Official Backstop Events** concept to the Fiscal/Treasury Stress model.
- Backstops can reduce immediate crisis/liquidity risk while increasing evidence of structural fragility.
- The model should separately represent immediate liquidity relief and structural fiscal/Treasury stress.
- Examples: Fed swap lines, Treasury support programs, repo/funding facilities, foreign official support/liquidity arrangements, and foreign official Treasury selling pressure.

## Trade-candidate rules

Trade candidates are not orders.

A trade candidate should include thesis, supporting factors, contradictory evidence, entry trigger, invalidation, catalyst, risk notes, max-loss concept, time horizon, and approval status.

Allowed statuses:

- Pending review.
- Approved for watchlist.
- Rejected.
- Needs more evidence.
- Needs model correction.

No candidate should imply automatic execution.

## Current model gaps

High-priority gaps:

- Current daily/regular data import needs to be reliable.
- Factor slope/acceleration needs to be explicit.
- Data freshness should be visible and penalize stale signals.
- Official Backstop Events are not yet implemented.
- Market Complacency should be expanded beyond static sample data.
- Assistant-readable model snapshot/report is needed so model output can be reviewed without relying on UI browsing.
