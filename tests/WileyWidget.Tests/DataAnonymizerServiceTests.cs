using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class DataAnonymizerServiceTests
{
    private readonly ILogger<DataAnonymizerService> _logger = LoggerFactory.Create(builder => { }).CreateLogger<DataAnonymizerService>();

    [Fact]
    public void AnonymizeEnterprise_MasksSensitiveFields_AndPreservesOperationalData()
    {
        var service = new DataAnonymizerService(_logger);
        var enterprise = new Enterprise
        {
            Id = 17,
            Name = "Town of Wiley Water",
            Type = "Water",
            Status = EnterpriseStatus.Suspended,
            CreatedDate = new DateTime(2025, 1, 1),
            ModifiedDate = new DateTime(2025, 1, 2),
            Description = "Contact jane@example.com or 555-123-4567. SSN 123-45-6789. Account 1234567890.",
            CurrentRate = 12.5m,
            CitizenCount = 40,
            MonthlyExpenses = 250m,
            TotalBudget = 1200m,
            BudgetAmount = 900m
        };

        var anonymized = service.AnonymizeEnterprise(enterprise);

        Assert.NotNull(anonymized);
        Assert.Equal(enterprise.Id, anonymized.Id);
        Assert.Equal(enterprise.Type, anonymized.Type);
        Assert.Equal(enterprise.Status, anonymized.Status);
        Assert.Equal(enterprise.CreatedDate, anonymized.CreatedDate);
        Assert.Equal(enterprise.ModifiedDate, anonymized.ModifiedDate);
        Assert.Equal(enterprise.CurrentRate, anonymized.CurrentRate);
        Assert.Equal(enterprise.CitizenCount, anonymized.CitizenCount);
        Assert.Equal(enterprise.MonthlyExpenses, anonymized.MonthlyExpenses);
        Assert.Equal(enterprise.TotalBudget, anonymized.TotalBudget);
        Assert.Equal(enterprise.BudgetAmount, anonymized.BudgetAmount);
        Assert.StartsWith("ANON_Enterprise_", anonymized.Name);
        Assert.Contains("[EMAIL_REDACTED]", anonymized.Description);
        Assert.Contains("[PHONE_REDACTED]", anonymized.Description);
        Assert.Contains("[SSN_REDACTED]", anonymized.Description);
    }

    [Fact]
    public void Anonymize_CollectionsAndNullValues_ReturnEmptyOrNullAsExpected()
    {
        var service = new DataAnonymizerService(_logger);

        Assert.Null(service.AnonymizeEnterprise(null!));
        Assert.Null(service.AnonymizeBudgetData(null!));
        Assert.Empty(service.AnonymizeEnterprises(null!).ToList());
        Assert.Empty(service.AnonymizeBudgetDataCollection(null!).ToList());
    }

    [Fact]
    public void Anonymize_UsesCache_AndClearCacheResetsStatistics()
    {
        var service = new DataAnonymizerService(_logger);

        var first = service.Anonymize("Alpha Utilities");
        var second = service.Anonymize("Alpha Utilities");

        Assert.Equal(first, second);
        Assert.Equal(1, service.GetCacheStatistics()["TotalEntries"]);

        service.ClearCache();

        Assert.Equal(0, service.GetCacheStatistics()["TotalEntries"]);
    }

    [Fact]
    public void AnonymizeBudgetData_PreservesFinancialFields()
    {
        var service = new DataAnonymizerService(_logger);
        var budgetData = new BudgetData
        {
            EnterpriseId = 9,
            FiscalYear = 2026,
            TotalBudget = 1000m,
            TotalExpenditures = 750m,
            RemainingBudget = 250m
        };

        var anonymized = service.AnonymizeBudgetData(budgetData);

        Assert.NotNull(anonymized);
        Assert.Equal(budgetData.EnterpriseId, anonymized.EnterpriseId);
        Assert.Equal(budgetData.FiscalYear, anonymized.FiscalYear);
        Assert.Equal(budgetData.TotalBudget, anonymized.TotalBudget);
        Assert.Equal(budgetData.TotalExpenditures, anonymized.TotalExpenditures);
        Assert.Equal(budgetData.RemainingBudget, anonymized.RemainingBudget);
    }
}