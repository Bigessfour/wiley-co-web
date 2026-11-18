using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using Xunit;

namespace WileyCoWeb.ComponentTests;

public sealed class AdoptedResidentialBillTests
{
    [Fact]
    public void October2026Bill_IsWaterSewageAndGarbage()
    {
        Assert.Equal(58.00m, AdoptedResidentialBill.Water);
        Assert.Equal(72.00m, AdoptedResidentialBill.Sewage);
        Assert.Equal(30.00m, AdoptedResidentialBill.Garbage);
        Assert.Equal(160.00m, AdoptedResidentialBill.TotalDue);
        Assert.Equal(16.00m, AdoptedResidentialBill.LatePenalty);
        Assert.Equal(176.00m, AdoptedResidentialBill.AfterDueDatePay);
        Assert.Equal(3_000, AdoptedResidentialBill.UsageGallons);
    }

    [Fact]
    public void AffordabilityResidentialBill_UsesTheOctober2026Total()
    {
        var residential = new AffordabilityAnalysisService()
            .GetDefaultClassDefinitions()
            .Single(definition => definition.ClassName == "Residential");

        Assert.Equal(AdoptedResidentialBill.TotalDue, residential.BaseMonthlyBill);
    }
}
