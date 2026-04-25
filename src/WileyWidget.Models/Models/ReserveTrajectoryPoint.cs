using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

public sealed record ReserveTrajectoryPoint(
    DateTime Month,
    decimal ProjectedReserve,
    decimal LowScenario,
    decimal HighScenario,
    decimal MinimumThreshold);

public sealed record ReserveTrajectoryForecastResult(
    decimal CurrentReserves,
    decimal RecommendedReserveLevel,
    string RiskAssessment,
    IReadOnlyList<ReserveTrajectoryPoint> Points);