using System.ComponentModel.DataAnnotations;
using WileyWidget.Models;

namespace WileyWidget.Tests;

public sealed class ModelValidationTests
{
    [Fact]
    public void ConnectionStringValidationAttribute_AllowsEmpty_WhenConfigured()
    {
        var attribute = new ConnectionStringValidationAttribute { AllowEmpty = true };
        var context = new ValidationContext(new object()) { MemberName = "DefaultConnection", DisplayName = "DefaultConnection" };

        var result = attribute.GetValidationResult(null, context);

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void ConnectionStringsOptions_Validates_ForRecognizedConnectionString()
    {
        var options = new ConnectionStringsOptions
        {
            DefaultConnection = "Host=localhost;Database=app;Username=user;Password=secret"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void ConnectionStringsOptions_ReportsError_ForInvalidConnectionString()
    {
        var options = new ConnectionStringsOptions
        {
            DefaultConnection = "Server=localhost"
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.ErrorMessage?.Contains("valid database connection string", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void QuickBooksOptions_ValidationReportsUrlAndEnvironmentErrors()
    {
        var options = new QuickBooksOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "not-a-url",
            Environment = "dev"
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.ErrorMessage == "QuickBooks.RedirectUri must be a valid URL");
        Assert.Contains(results, result => result.ErrorMessage?.Contains("sandbox", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void AppOptions_IsDarkMode_TracksThemeName()
    {
        var options = new AppOptions();

        Assert.True(options.IsDarkMode);

        options.Theme = "FluentLight";

        Assert.False(options.IsDarkMode);
    }

    [Fact]
    public void OverallBudget_Validate_ReportsMissingSnapshotDate_AndCalculatedValuesWork()
    {
        var budget = new OverallBudget
        {
            TotalMonthlyRevenue = 100m,
            TotalMonthlyExpenses = 75m,
            TotalMonthlyBalance = 25m,
            TotalCitizensServed = 10,
            IsCurrent = true
        };

        var results = Validate(budget);

        Assert.Contains(results, result => result.ErrorMessage == "Snapshot date must be set to a valid date");
        Assert.True(budget.IsSurplus);
        Assert.Equal(25m, budget.DeficitPercentage);
    }

    [Fact]
    public void ImportProgress_PercentComplete_ReturnsExpectedPercentage()
    {
        var progress = new ImportProgress
        {
            TotalRows = 20,
            ProcessedRows = 5
        };

        Assert.Equal(25, progress.PercentComplete);
    }

    [Fact]
    public void BudgetImportOptions_UsesExpectedDefaultFlags()
    {
        var options = new BudgetImportOptions();

        Assert.True(options.ValidateData);
        Assert.True(options.ValidateGASBCompliance);
        Assert.False(options.OverwriteExisting);
        Assert.False(options.PreviewOnly);
        Assert.False(options.CreateNewBudgetPeriod);
        Assert.False(options.OverwriteExistingAccounts);
        Assert.Null(options.DefaultFundType);
        Assert.Null(options.FiscalYear);
        Assert.Null(options.BudgetYear);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(instance);

        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        return results;
    }
}