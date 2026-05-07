# Macro Regime Factor Monitor

A small .NET 8 MVP that scores macro and market factors, aggregates them into category signals, and classifies the current macro regime from a weighted composite score.

## Development environment

This repository includes a Dev Container definition that uses the official .NET 8 SDK image:

```json
"image": "mcr.microsoft.com/devcontainers/dotnet:8.0"
```

Open the repository in a Dev Container so `dotnet` is available for restore, build, and run commands.

## Run the MVP

```bash
dotnet run --project src/MacroRegimeFactorMonitor/MacroRegimeFactorMonitor.csproj
```

By default, the app reads `data/sample-factors.csv`. To evaluate another factor file, pass its path as the first argument:

```bash
dotnet run --project src/MacroRegimeFactorMonitor/MacroRegimeFactorMonitor.csproj -- path/to/factors.csv
```

## Input format

The monitor expects a CSV with the following columns:

| Column | Description |
| --- | --- |
| `Date` | Observation date. The monitor evaluates the latest date in the file. |
| `Factor` | Human-readable factor name. |
| `Category` | Group used for category-level aggregation. |
| `Value` | Current factor value. |
| `Baseline` | Neutral or expected value for the factor. |
| `Volatility` | Scaling denominator used to convert the factor into a z-score. |
| `HigherIsRiskOn` | `true` when higher values are constructive for risk assets; `false` when lower values are constructive. |
| `Weight` | Contribution multiplier applied to the factor z-score. |

## Scoring approach

For each latest-date factor, the MVP computes:

```text
z-score = ((Value - Baseline) / Volatility) * direction
contribution = z-score * Weight
```

`direction` is `1` when `HigherIsRiskOn` is true and `-1` otherwise. Contributions are summed into category scores and a total composite score.

## Regime thresholds

| Composite score | Regime |
| ---: | --- |
| `>= 1.5` | Expansion / Risk-On |
| `>= 0.4` | Constructive Growth |
| `<= -1.5` | Contraction / Risk-Off |
| `<= -0.4` | Defensive Slowdown |
| otherwise | Neutral / Transition |
