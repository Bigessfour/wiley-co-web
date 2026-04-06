using System.ComponentModel.DataAnnotations;
using WileyWidget.Models.Entities;
using WileyWidget.Models;

namespace WileyWidget.Tests;

public sealed class ModelBehaviorTests
{
    [Fact]
    public void BudgetEntry_ComputedProperties_ReflectFundAndAmounts()
    {
        var entry = new BudgetEntry
        {
            BudgetedAmount = 120m,
            ActualAmount = 45m,
            Variance = -15m,
            Fund = new Fund { Name = "Town of Wiley General Fund" },
            Department = new Department { Name = "Finance" },
            MunicipalAccount = new MunicipalAccount { Name = "Water Billing", Type = AccountType.Asset },
            FundType = FundType.GeneralFund
        };

        Assert.Equal(120m, entry.TotalBudget);
        Assert.Equal(45m, entry.ActualSpent);
        Assert.Equal(75m, entry.Remaining);
        Assert.Equal(37.5m, entry.PercentOfBudget);
        Assert.Equal(0.3750m, entry.PercentOfBudgetFraction);
        Assert.Equal(75m, entry.RemainingAmount);
        Assert.Equal(0.6250m, entry.PercentRemainingFraction);
        Assert.Equal("Town of Wiley General Fund", entry.EntityName);
        Assert.Equal("Water Billing", entry.AccountName);
        Assert.Equal("Asset", entry.AccountTypeName);
        Assert.Equal("Finance", entry.DepartmentName);
        Assert.Equal("GeneralFund", entry.FundTypeDescription);
        Assert.Equal(-15m, entry.VarianceAmount);
        Assert.Equal(-0.1250m, entry.VariancePercentage);
        Assert.Equal(120m, entry.TownOfWileyBudgetedAmount);
        Assert.Equal(45m, entry.TownOfWileyActualAmount);
        Assert.Equal(0m, entry.WsdBudgetedAmount);
        Assert.Equal(0m, entry.WsdActualAmount);
    }

    [Fact]
    public void BudgetEntry_AccountNameSetter_UpdatesDescription()
    {
        var entry = new BudgetEntry();

        entry.AccountName = "Updated description";

        Assert.Equal("Updated description", entry.Description);
    }

    [Fact]
    public void UtilityCustomer_RaisesNotifications_AndValidatesBadFields()
    {
        var customer = new UtilityCustomer();

        customer.AccountNumber = "12345";
        customer.FirstName = "Jane";
        customer.LastName = "Doe";
        customer.CompanyName = "  Wiley Utilities  ";
        customer.CustomerType = CustomerType.Commercial;
        customer.ServiceLocation = ServiceLocation.OutsideCityLimits;
        customer.Status = CustomerStatus.Inactive;
        customer.ServiceAddress = "1 Main St";
        customer.ServiceCity = "Wiley";
        customer.ServiceState = "TX";
        customer.ServiceZipCode = "12345";
        customer.EmailAddress = "not-an-email";
        customer.PhoneNumber = "abc";
        customer.MailingState = "XYZ";
        customer.MailingZipCode = "12";
        customer.TaxId = "123";
        customer.BusinessLicenseNumber = "1234";

        Assert.Equal("Wiley Utilities", customer.CompanyName);
        Assert.Equal("Wiley Utilities", customer.DisplayName);

        var results = Validate(customer);
        var validationMessages = string.Join("\n", results.Select(result => result.ErrorMessage));

        Assert.Contains(results, result => result.ErrorMessage == "Invalid email address format");
        Assert.Contains(results, result => result.ErrorMessage == "Phone number must contain only digits, spaces, parentheses, or dashes");
        Assert.Contains("Mailing state", validationMessages, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(results, result => result.ErrorMessage == "Mailing ZIP code must be 5 digits or ZIP+4");
        Assert.Contains(results, result => result.ErrorMessage == "Tax ID must be 9 digits or in the format 12-3456789");
        Assert.Contains(results, result => result.ErrorMessage == "Business license number must be at least 5 characters");
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(instance);
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        if (instance is IValidatableObject validatable)
        {
            results.AddRange(validatable.Validate(context));
        }

        return results;
    }
}