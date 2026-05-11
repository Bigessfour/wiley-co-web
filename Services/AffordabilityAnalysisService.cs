using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class AffordabilityAnalysisService
{
    private static readonly IReadOnlyList<AffordabilityCustomerClassDefinition> DefaultClassDefinitions =
    [
        new("Residential", 0.66m, 128m, "#0ea5e9"),
        new("Multi-Family", 0.12m, 96m, "#22c55e"),
        new("Commercial", 0.17m, 214m, "#f59e0b"),
        new("Industrial", 0.05m, 468m, "#ef4444")
    ];

    private static readonly IReadOnlyList<AffordabilityRateScenario> DefaultScenarios =
    [
        new("Baseline", 1.00m, "Current modeled monthly bill"),
        new("+5%", 1.05m, "Moderate rate pressure"),
        new("+10%", 1.10m, "Stress-test rate pressure"),
        new("+15%", 1.15m, "Upper-bound rate pressure")
    ];

    public AffordabilityDashboardSnapshot BuildDashboard(
        IReadOnlyList<CustomerRow> customers,
        decimal monthlyMhi,
        IReadOnlyList<AffordabilityCustomerClassDefinition>? classDefinitions = null,
        IReadOnlyList<AffordabilityRateScenario>? rateScenarios = null)
    {
        var normalizedMhi = Math.Max(monthlyMhi, 1m);
        var definitions = NormalizeClassDefinitions(classDefinitions);
        var scenarios = NormalizeRateScenarios(rateScenarios);
        var customerCount = customers.Count;

        var classSnapshots = BuildClassSnapshots(definitions, customerCount);
        var scenarioPoints = BuildScenarioPoints(classSnapshots, scenarios, normalizedMhi);
        var averageMonthlyBill = classSnapshots.Sum(snapshot => snapshot.WeightedMonthlyBill);
        var averageBillPctOfMhi = averageMonthlyBill / normalizedMhi * 100m;
        var (riskBand, riskNarrative) = DetermineRiskBand(averageBillPctOfMhi);

        return new AffordabilityDashboardSnapshot(
            new AffordabilitySummary(
                normalizedMhi,
                customerCount,
                averageMonthlyBill,
                averageBillPctOfMhi,
                riskBand,
                riskNarrative),
            classSnapshots,
            scenarios,
            scenarioPoints);
    }

    public IReadOnlyList<AffordabilityCustomerClassDefinition> GetDefaultClassDefinitions() => DefaultClassDefinitions;

    public IReadOnlyList<AffordabilityRateScenario> GetDefaultRateScenarios() => DefaultScenarios;

    private static IReadOnlyList<AffordabilityCustomerClassDefinition> NormalizeClassDefinitions(IReadOnlyList<AffordabilityCustomerClassDefinition>? classDefinitions)
    {
        if (classDefinitions is { Count: > 0 })
        {
            return classDefinitions;
        }

        return DefaultClassDefinitions;
    }

    private static IReadOnlyList<AffordabilityRateScenario> NormalizeRateScenarios(IReadOnlyList<AffordabilityRateScenario>? rateScenarios)
    {
        if (rateScenarios is { Count: > 0 })
        {
            return rateScenarios;
        }

        return DefaultScenarios;
    }

    private static IReadOnlyList<AffordabilityCustomerClassSnapshot> BuildClassSnapshots(
        IReadOnlyList<AffordabilityCustomerClassDefinition> definitions,
        int customerCount)
    {
        var snapshots = new List<AffordabilityCustomerClassSnapshot>(definitions.Count);
        var remainingCustomers = customerCount;

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var estimatedCount = index == definitions.Count - 1
                ? Math.Max(remainingCustomers, 0)
                : Math.Max((int)Math.Round(customerCount * definition.Share, MidpointRounding.AwayFromZero), 0);

            remainingCustomers -= estimatedCount;

            snapshots.Add(new AffordabilityCustomerClassSnapshot(
                definition.ClassName,
                definition.Share,
                estimatedCount,
                definition.BaseMonthlyBill,
                definition.BaseMonthlyBill * definition.Share,
                definition.AccentColor));
        }

        return snapshots;
    }

    private static IReadOnlyList<AffordabilityScenarioPoint> BuildScenarioPoints(
        IReadOnlyList<AffordabilityCustomerClassSnapshot> classSnapshots,
        IReadOnlyList<AffordabilityRateScenario> scenarios,
        decimal monthlyMhi)
    {
        var points = new List<AffordabilityScenarioPoint>(classSnapshots.Count * scenarios.Count);

        for (var scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
        {
            var scenario = scenarios[scenarioIndex];

            foreach (var classSnapshot in classSnapshots)
            {
                var monthlyBill = classSnapshot.BaseMonthlyBill * scenario.RateMultiplier;
                points.Add(new AffordabilityScenarioPoint(
                    classSnapshot.ClassName,
                    scenario.ScenarioName,
                    monthlyBill,
                    monthlyBill / monthlyMhi * 100m,
                    classSnapshot.AccentColor,
                    scenarioIndex));
            }
        }

        return points;
    }

    private static (string RiskBand, string RiskNarrative) DetermineRiskBand(decimal averageBillPctOfMhi)
    {
        if (averageBillPctOfMhi < 4m)
        {
            return ("Manageable", "Average monthly burden stays comfortably below common affordability thresholds.");
        }

        if (averageBillPctOfMhi < 6m)
        {
            return ("Monitor", "Average monthly burden is creeping toward the affordability watch zone.");
        }

        if (averageBillPctOfMhi < 8m)
        {
            return ("Elevated", "Average monthly burden is now high enough to merit rate design review.");
        }

        return ("Stressed", "Average monthly burden is above the preferred affordability ceiling.");
    }
}