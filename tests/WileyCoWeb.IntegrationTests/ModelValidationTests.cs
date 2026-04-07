using WileyWidget.Models;
using WileyWidget.Models.DTOs;
using WileyWidget.Models.Validators;
using WileyWidget.Business.Configuration;

namespace WileyCoWeb.IntegrationTests;

public sealed class ModelValidationTests
{
    [Fact]
    public void AccountNumber_ExposesHierarchyMetadata()
    {
        var accountNumber = new AccountNumber("410.2.1");

        Assert.Equal("410.2.1", accountNumber.Value);
        Assert.Equal(3, accountNumber.Level);
        Assert.Equal("410.2", accountNumber.ParentNumber);
        Assert.False(accountNumber.IsParent);
        Assert.Equal("410.2", accountNumber.GetParentNumber());
    }

    [Fact]
    public void EnterpriseSummary_AndMunicipalAccountSummary_CalculateExpectedVariance()
    {
        var enterpriseSummary = new EnterpriseSummary
        {
            Id = 1,
            Name = "Water Utility",
            CurrentRate = 55.25m,
            MonthlyRevenue = 13260m,
            MonthlyExpenses = 13250m,
            MonthlyBalance = 10m,
            CitizenCount = 240
        };

        var accountSummary = new MunicipalAccountSummary
        {
            Id = 2,
            AccountNumber = "410.1",
            Name = "Water Revenue",
            AccountType = "Revenue",
            Balance = 1500m,
            BudgetAmount = 1200m
        };

        Assert.Equal(10m, enterpriseSummary.MonthlyBalance);
        Assert.Equal(300m, accountSummary.Variance);
        Assert.Equal("Active", enterpriseSummary.Status);
    }

    [Fact]
    public void BudgetAndEnterpriseValidators_ReturnExpectedResults()
    {
        var budgetValidator = new BudgetDataValidator();
        var enterpriseValidator = new EnterpriseValidator();

        var validBudget = budgetValidator.Validate(new BudgetData
        {
            EnterpriseId = 1,
            FiscalYear = 2026,
            TotalBudget = 5000m,
            TotalExpenditures = 4200m,
            RemainingBudget = 800m
        });

        var invalidEnterprise = enterpriseValidator.Validate(new Enterprise
        {
            Name = string.Empty,
            CurrentRate = 0m,
            MonthlyExpenses = -1m,
            CitizenCount = 0
        });

        Assert.True(validBudget.IsValid);
        Assert.False(invalidEnterprise.IsValid);
        Assert.NotEmpty(invalidEnterprise.Errors);
    }

    [Fact]
    public void AccountTypeValidator_ValidatesAccountAndFundClassRules()
    {
        var validAccount = new MunicipalAccount
        {
            Id = 10,
            Name = "Cash Reserve",
            AccountNumber = new AccountNumber("101"),
            Type = AccountType.Cash,
            FundType = MunicipalFundType.General
        };

        var invalidAccount = new MunicipalAccount
        {
            Id = 11,
            Name = "Wrongly Classified",
            AccountNumber = new AccountNumber("210"),
            Type = AccountType.Revenue,
            FundType = MunicipalFundType.General
        };

        var validResult = AccountTypeValidator.ValidateAccountTypeCompliance(new[] { validAccount });
        var invalidResult = AccountTypeValidator.ValidateAccountTypeCompliance(new[] { invalidAccount });

        Assert.True(validResult.IsValid);
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Errors, error => error.Contains("Wrongly Classified", StringComparison.Ordinal));
    }
}