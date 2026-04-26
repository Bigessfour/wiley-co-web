namespace WileyCoWeb.Contracts;

public sealed record AffordabilityCustomerClassDefinition(
    string ClassName,
    decimal Share,
    decimal BaseMonthlyBill,
    string AccentColor);

public sealed record AffordabilityRateScenario(
    string ScenarioName,
    decimal RateMultiplier,
    string Description);

public sealed record AffordabilityCustomerClassSnapshot(
    string ClassName,
    decimal Share,
    int EstimatedCustomerCount,
    decimal BaseMonthlyBill,
    decimal WeightedMonthlyBill,
    string AccentColor);

public sealed record AffordabilityScenarioPoint(
    string ClassName,
    string ScenarioName,
    decimal MonthlyBill,
    decimal MonthlyBillPctOfMhi,
    string AccentColor,
    int ScenarioIndex);

public sealed record AffordabilitySummary(
    decimal MonthlyMhi,
    int CustomerCount,
    decimal AverageMonthlyBill,
    decimal AverageBillPctOfMhi,
    string RiskBand,
    string RiskNarrative);

public sealed record AffordabilityDashboardSnapshot(
    AffordabilitySummary Summary,
    IReadOnlyList<AffordabilityCustomerClassSnapshot> CustomerClasses,
    IReadOnlyList<AffordabilityRateScenario> RateScenarios,
    IReadOnlyList<AffordabilityScenarioPoint> ScenarioPoints);