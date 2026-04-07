using System.ComponentModel;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Configuration;
using WileyWidget.Models;
using WileyWidget.Models.DTOs;
using WileyWidget.Services;
using WileyWidget.Services.Logging;

namespace WileyWidget.Tests;

public sealed class ValidationAndUtilityTests
{
    private readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder => { });

    [Fact]
    public void AccountTypeValidator_ReturnsExpectedMatches_ForAccountNumberAndFund()
    {
        var validator = new WileyWidget.Business.Configuration.AccountTypeValidator();

        Assert.True(validator.ValidateAccountTypeForNumber(AccountType.Cash, "101.1"));
        Assert.True(validator.ValidateAccountTypeForFund(AccountType.Fees, FundClass.Proprietary));
        Assert.False(validator.ValidateAccountTypeForFund(AccountType.Taxes, FundClass.Proprietary));

        var validTypes = validator.GetValidAccountTypesForNumber("410.2").ToArray();
        Assert.Contains(AccountType.Revenue, validTypes);
        Assert.Contains(AccountType.Fees, validator.GetValidAccountTypesForFund(FundClass.Governmental));
    }

    [Fact]
    public void AccountTypeValidator_ReportsErrors_ForInvalidAccount()
    {
        var validator = new WileyWidget.Business.Configuration.AccountTypeValidator();

        var errors = validator.ValidateAccount(new MunicipalAccount
        {
            Id = 7,
            Name = "Badly Classified",
            AccountNumber = new AccountNumber("410.1"),
            Type = AccountType.Payables,
            FundType = MunicipalFundType.General
        });

        Assert.Single(errors);
        Assert.Contains("not valid for account number", errors[0]);
    }

    [Fact]
    public void AccountTypeValidator_ComplianceCheck_ReturnsInvalid_ForMixedAccounts()
    {
        var validator = new WileyWidget.Business.Configuration.AccountTypeValidator();

        var result = validator.ValidateAccountTypeCompliance(new[]
        {
            new MunicipalAccount
            {
                Id = 1,
                Name = "Valid Cash",
                AccountNumber = new AccountNumber("101"),
                Type = AccountType.Cash,
                FundType = MunicipalFundType.General
            },
            new MunicipalAccount
            {
                Id = 2,
                Name = "Invalid Revenue",
                AccountNumber = new AccountNumber("210"),
                Type = AccountType.Revenue,
                FundType = MunicipalFundType.General
            }
        });

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, error => error.Contains("Invalid Revenue", StringComparison.Ordinal));
    }

    [Fact]
    public void BudgetAndEnterpriseValidators_ReturnExpectedValidationResults()
    {
        var budgetValidator = new WileyWidget.Models.Validators.BudgetDataValidator();
        var enterpriseValidator = new WileyWidget.Models.Validators.EnterpriseValidator();

        var validBudget = budgetValidator.Validate(new BudgetData
        {
            EnterpriseId = 1,
            FiscalYear = 2026,
            TotalBudget = 1250m,
            TotalExpenditures = 250m,
            RemainingBudget = 1000m
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
    public void LogPathResolver_CreatesLogsDirectory_BelowRepoRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wiley-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "WileyWidget.sln"), string.Empty);

        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            var logsDirectory = LogPathResolver.GetLogsDirectory();

            Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "logs"), logsDirectory);
            Assert.True(Directory.Exists(logsDirectory));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void DataAnonymizerService_MasksSensitiveData_AndCachesDeterministically()
    {
        var service = new DataAnonymizerService(loggerFactory.CreateLogger<DataAnonymizerService>());

        var enterprise = new Enterprise
        {
            Id = 42,
            Name = "Water Utility",
            Type = "Utility",
            Status = EnterpriseStatus.Active,
            Description = "Contact ada@wiley.co or 555-123-4567, SSN 123-45-6789, account 12345678.",
            CurrentRate = 55.25m,
            CitizenCount = 240,
            MonthlyExpenses = 13250m,
            TotalBudget = 100000m,
            BudgetAmount = 75000m
        };

        var anonymized = service.AnonymizeEnterprise(enterprise);
        var firstToken = service.Anonymize("Town of Wiley");
        var secondToken = service.Anonymize("Town of Wiley");

        Assert.NotNull(anonymized);
        Assert.StartsWith("ANON_Enterprise_", anonymized!.Name, StringComparison.Ordinal);
        Assert.Contains("[EMAIL_REDACTED]", anonymized.Description, StringComparison.Ordinal);
        Assert.Contains("[PHONE_REDACTED]", anonymized.Description, StringComparison.Ordinal);
        Assert.Contains("[SSN_REDACTED]", anonymized.Description, StringComparison.Ordinal);
        Assert.Contains("[ACCOUNT_REDACTED]", anonymized.Description, StringComparison.Ordinal);
        Assert.Equal(enterprise.CurrentRate, anonymized.CurrentRate);
        Assert.Equal(enterprise.CitizenCount, anonymized.CitizenCount);
        Assert.Equal(enterprise.MonthlyExpenses, anonymized.MonthlyExpenses);
        Assert.Equal(firstToken, secondToken);
        Assert.Equal(2, service.GetCacheStatistics()["TotalEntries"]);
    }

    [Fact]
    public void DataAnonymizerService_ClearsCache()
    {
        var service = new DataAnonymizerService(loggerFactory.CreateLogger<DataAnonymizerService>());

        service.Anonymize("Alpha");
        service.ClearCache();

        Assert.Equal(0, service.GetCacheStatistics()["TotalEntries"]);
    }
}