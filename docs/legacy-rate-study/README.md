# Legacy rate-study engine (reference only)

Source preserved from [Bigessfour/Wiley-Widget](https://github.com/Bigessfour/Wiley-Widget) commit [`6a9cbcdd`](https://github.com/Bigessfour/Wiley-Widget/commit/6a9cbcdd7bf4e34e1e8a4d79b12c337974983178). Files use the `.cs.txt` extension so they are **not compiled** by any project in this solution.

## What the engine did (WinForms era)

| Component | Role |
|-----------|------|
| `ServiceChargeCalculatorService.cs.txt` | Recommended monthly service charges from enterprise expenses, with **budget validation**, coverage ratio checks, **reserve** targets (~10%), and **debt-service caps** (~20% of revenue). |
| `WhatIfScenarioEngine.cs.txt` | **What-if scenarios**: pay raises, benefits, equipment purchases/financing, reserve adjustments; aggregates impacts, recommendations, and **risk assessment** (Low/Medium/High). |
| `RateScenarioTools.cs.txt` | Semantic Kernel plugin surface for read-only rate/scenario queries. |
| `RecommendedMonthlyChargeViewModel.cs.txt` | Dashboard VM tying department expenses, benchmarks, and recommendations. |
| `IChargeCalculatorService.cs.txt`, `IWhatIfScenarioEngine.cs.txt`, `ServiceChargeRecommendation.cs.txt` | Contracts and DTOs used by the services above. |

Supporting types for scenarios (`ScenarioParameters`, `ComprehensiveScenario`, `RiskAssessment`, etc.) are defined at the bottom of `WhatIfScenarioEngine.cs.txt` in the legacy tree.

## Relationship to wiley-co-web

The Blazor workspace implements a **partial port** of this behavior:

- Client calculation path: `State/RateCalculator.cs` (forwards to domain rates).
- UI: `Components/Panels/ScenarioPlannerPanel.razor`.

Domain rate math in this repo is centered on `src/WileyWidget.Abstractions/EnterpriseRateService.cs` (see repository `AGENTS.md`). Use these legacy files when comparing behavior or recovering WinForms-era business rules not yet ported.
