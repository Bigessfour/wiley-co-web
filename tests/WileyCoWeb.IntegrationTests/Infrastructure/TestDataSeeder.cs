using WileyWidget.Models.Amplify;

namespace WileyCoWeb.IntegrationTests.Infrastructure;

internal static class TestDataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        var utilitiesDepartment = new Department
        {
            Name = "Utilities",
            DepartmentCode = "UTIL"
        };

        var streetsDepartment = new Department
        {
            Name = "Streets",
            DepartmentCode = "STR"
        };

        context.Departments.AddRange(utilitiesDepartment, streetsDepartment);

        context.Enterprises.AddRange(
            new Enterprise
            {
                Name = "Water Utility",
                Type = "Water",
                CurrentRate = 55.25m,
                MonthlyExpenses = 13250m,
                CitizenCount = 240,
                IsDeleted = false
            },
            new Enterprise
            {
                Name = "Wiley Sanitation District",
                Type = "Sewer",
                CurrentRate = 41.10m,
                MonthlyExpenses = 9100m,
                CitizenCount = 180,
                IsDeleted = false
            },
            new Enterprise
            {
                Name = "Trash",
                Type = "Trash",
                CurrentRate = 24.50m,
                MonthlyExpenses = 7600m,
                CitizenCount = 160,
                IsDeleted = false
            },
            new Enterprise
            {
                Name = "Apartments",
                Type = "Apartments",
                CurrentRate = 725.00m,
                MonthlyExpenses = 14500m,
                CitizenCount = 24,
                IsDeleted = false
            },
            new Enterprise
            {
                Name = "Archived Utility",
                CurrentRate = 12m,
                MonthlyExpenses = 800m,
                CitizenCount = 20,
                IsDeleted = true
            });

        context.UtilityCustomers.AddRange(
            new UtilityCustomer
            {
                AccountNumber = "1001",
                FirstName = "Ada",
                LastName = "Lovelace",
                CustomerType = CustomerType.Residential,
                ServiceAddress = "1 Main St",
                ServiceCity = "Wiley",
                ServiceState = "CO",
                ServiceZipCode = "81092",
                ServiceLocation = ServiceLocation.InsideCityLimits,
                Notes = "Water Utility seeded integration customer"
            },
            new UtilityCustomer
            {
                AccountNumber = "1002",
                FirstName = "Grace",
                LastName = "Hopper",
                CompanyName = "Harbor Foods",
                CustomerType = CustomerType.Commercial,
                ServiceAddress = "2 Market St",
                ServiceCity = "Wiley",
                ServiceState = "CO",
                ServiceZipCode = "81092",
                ServiceLocation = ServiceLocation.OutsideCityLimits,
                Notes = "Water Utility seeded integration customer"
            });

        context.DepartmentCurrentCharges.AddRange(
            new DepartmentCurrentCharge
            {
                Department = "Water",
                CurrentCharge = 55.25m,
                CustomerCount = 240,
                UpdatedBy = nameof(TestDataSeeder),
                Notes = "Seeded from Water Utility.",
                IsActive = true
            },
            new DepartmentCurrentCharge
            {
                Department = "Sewer",
                CurrentCharge = 41.10m,
                CustomerCount = 180,
                UpdatedBy = nameof(TestDataSeeder),
                Notes = "Seeded from Wiley Sanitation District.",
                IsActive = true
            },
            new DepartmentCurrentCharge
            {
                Department = "Trash",
                CurrentCharge = 24.50m,
                CustomerCount = 160,
                UpdatedBy = nameof(TestDataSeeder),
                Notes = "Seeded from Trash.",
                IsActive = true
            },
            new DepartmentCurrentCharge
            {
                Department = "Apartments",
                CurrentCharge = 725.00m,
                CustomerCount = 24,
                UpdatedBy = nameof(TestDataSeeder),
                Notes = "Seeded from Apartments.",
                IsActive = true
            });

        context.DepartmentGoals.AddRange(
            new DepartmentGoal
            {
                Department = "Water",
                AdjustmentFactor = 1.02m,
                TargetProfitMarginPercent = 2.00m,
                RecommendationText = "Water Utility should stand on its own rates.",
                Source = nameof(TestDataSeeder),
                IsActive = true
            },
            new DepartmentGoal
            {
                Department = "Sewer",
                AdjustmentFactor = 1.01m,
                TargetProfitMarginPercent = 1.00m,
                RecommendationText = "Wiley Sanitation District should stand on its own rates.",
                Source = nameof(TestDataSeeder),
                IsActive = true
            },
            new DepartmentGoal
            {
                Department = "Trash",
                AdjustmentFactor = 1.03m,
                TargetProfitMarginPercent = 3.00m,
                RecommendationText = "Trash should stand on its own rates.",
                Source = nameof(TestDataSeeder),
                IsActive = true
            },
            new DepartmentGoal
            {
                Department = "Apartments",
                AdjustmentFactor = 1.04m,
                TargetProfitMarginPercent = 4.00m,
                RecommendationText = "Apartments should stand on their own rents and fees.",
                Source = nameof(TestDataSeeder),
                IsActive = true
            });

        context.BudgetEntries.AddRange(
            new BudgetEntry
            {
                AccountNumber = "410.1",
                Description = "Water operations",
                BudgetedAmount = 100000m,
                ActualAmount = 94000m,
                FiscalYear = 2026,
                StartPeriod = new DateTime(2025, 7, 1),
                EndPeriod = new DateTime(2026, 6, 30),
                FundType = FundType.EnterpriseFund,
                Department = utilitiesDepartment,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "410.2",
                Description = "Water capital",
                BudgetedAmount = 60000m,
                ActualAmount = 20000m,
                FiscalYear = 2026,
                StartPeriod = new DateTime(2025, 7, 1),
                EndPeriod = new DateTime(2026, 6, 30),
                FundType = FundType.EnterpriseFund,
                Department = utilitiesDepartment,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "510.1",
                Description = "Street maintenance",
                BudgetedAmount = 40000m,
                ActualAmount = 15000m,
                FiscalYear = 2026,
                StartPeriod = new DateTime(2025, 7, 1),
                EndPeriod = new DateTime(2026, 6, 30),
                FundType = FundType.GeneralFund,
                Department = streetsDepartment,
                IsGASBCompliant = true
            },
            new BudgetEntry
            {
                AccountNumber = "410.3",
                Description = "Prior year utility budget",
                BudgetedAmount = 50000m,
                ActualAmount = 48000m,
                FiscalYear = 2025,
                StartPeriod = new DateTime(2024, 7, 1),
                EndPeriod = new DateTime(2025, 6, 30),
                FundType = FundType.EnterpriseFund,
                Department = utilitiesDepartment,
                IsGASBCompliant = true
            });

        await context.SaveChangesAsync();
    }
}