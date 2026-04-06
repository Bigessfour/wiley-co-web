using WileyWidget.Models;
using WileyWidget.Business.Configuration;

namespace WileyWidget.Tests;

public sealed class AccountTypeValidatorTests
{
    [Fact]
    public void ValidateAccountTypeForNumber_RecognizesValidAndInvalidRanges()
    {
        Assert.True(AccountTypeValidator.ValidateAccountTypeForNumber(AccountType.Cash, "101"));
        Assert.False(AccountTypeValidator.ValidateAccountTypeForNumber(AccountType.Revenue, "101"));
    }

    [Fact]
    public void ValidateAccountTypeForFund_RecognizesAllowedFundClasses()
    {
        Assert.True(AccountTypeValidator.ValidateAccountTypeForFund(AccountType.Sales, FundClass.Proprietary));
        Assert.False(AccountTypeValidator.ValidateAccountTypeForFund(AccountType.Taxes, FundClass.Proprietary));
    }

    [Fact]
    public void ValidateAccount_ReturnsExpectedErrors_ForInvalidAccount()
    {
        var account = new MunicipalAccount
        {
            Id = 42,
            Name = "Water revenue",
            AccountNumber = new AccountNumber("101"),
            Type = AccountType.Revenue,
            FundType = MunicipalFundType.Trust
        };

    var errors = AccountTypeValidator.ValidateAccount(account);

        Assert.Contains(errors, error => error.Contains("not valid for account number", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("not allowed in fund class", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAccount_ReturnsMissingNumberError_WhenAccountNumberIsMissing()
    {
        var account = new MunicipalAccount
        {
            Id = 7,
            Name = "Missing number",
            Type = AccountType.Cash,
            FundType = MunicipalFundType.General
        };

    var errors = AccountTypeValidator.ValidateAccount(account);

        Assert.Single(errors);
        Assert.Contains("missing account number", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAccountTypeCompliance_ThrowsForNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => AccountTypeValidator.ValidateAccountTypeCompliance(null!));
    }

    [Fact]
    public void GetValidAccountTypesForNumber_ContainsExpectedAccountTypes()
    {
        var accountTypes = AccountTypeValidator.GetValidAccountTypesForNumber("101").ToList();

        Assert.Contains(AccountType.Cash, accountTypes);
        Assert.Contains(AccountType.Investments, accountTypes);
        Assert.Contains(AccountType.Receivables, accountTypes);
        Assert.DoesNotContain(AccountType.Revenue, accountTypes);
    }
}