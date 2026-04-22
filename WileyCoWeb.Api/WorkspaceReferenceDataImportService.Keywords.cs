using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{
    static WorkspaceReferenceDataImportService()
    {
    }

    private static readonly string[] CorporateKeywords =
    [
        "LLC",
        "INC",
        "CORP",
        "COMPANY",
        "COBANK",
        "BANK",
        "TREE",
        "TRIMMING",
        "SERVICE",
        "SERVICES",
        "UTILITY",
        "DISTRICT",
        "TOWN OF",
        "CITY OF",
        "STATE OF",
        "COUNTY OF",
        "RANCH",
        "FARM",
        "SHOP",
        "STORE",
        "MARKET"
    ];

    private static readonly string[] GovernmentKeywords =
    [
        "TOWN OF",
        "CITY OF",
        "STATE OF",
        "COUNTY OF",
        "DISTRICT"
    ];

    private static readonly string[] InstitutionalKeywords =
    [
        "SCHOOL",
        "CHURCH",
        "HOSPITAL",
        "LIBRARY"
    ];

    private static readonly string[] MultiFamilyKeywords =
    [
        "APARTMENT",
        "APTS",
        "MOBILE HOME",
        "TRAILER PARK"
    ];

    private static readonly (string Pattern, string? Type)[] EnterpriseTypeOverrides =
    [
        ("sanitation", "Sewer"),
        ("sewer", "Sewer"),
        ("trash", "Trash")
    ];

    private static readonly Func<string, bool>[] IndividualNameRules =
    [
        HasAllowedIndividualNameCharacters,
        HasNoCorporateKeywords,
        HasValidIndividualNameTokens
    ];

    private static readonly Func<string, CustomerType?>[] CustomerTypeResolvers =
    [
        ResolveGovernmentCustomerType,
        ResolveInstitutionalCustomerType,
        ResolveMultiFamilyCustomerType,
        ResolveDefaultCustomerTypeAsNullable
    ];

    private static readonly Func<string, string, ParsedCustomerIdentity?>[] CustomerIdentityFactories =
    [
        TryCreateResidentialCustomerIdentity,
        TryCreateContactCustomerIdentity,
        (displayName, _) => CreateDefaultCustomerIdentity(displayName)
    ];

    private static readonly (Func<string, bool> Predicate, CustomerType? Type)[] CustomerTypeOverrides =
    [
        (IsGovernmentCustomer, CustomerType.Government),
        (IsInstitutionalCustomer, CustomerType.Institutional),
        (IsMultiFamilyCustomer, CustomerType.MultiFamily)
    ];

    private static readonly IReadOnlyDictionary<string, EnterpriseBaselineSeed> DefaultEnterpriseBaselines =
        WorkspaceEnterpriseSeedCatalog.All
                .SelectMany(seed => new[]
                {
                    new KeyValuePair<string, EnterpriseBaselineSeed>(
                        seed.Name,
                        new EnterpriseBaselineSeed(seed.CurrentRate, seed.MonthlyExpenses, seed.CustomerCount)),
                    new KeyValuePair<string, EnterpriseBaselineSeed>(
                        seed.DepartmentName,
                        new EnterpriseBaselineSeed(seed.CurrentRate, seed.MonthlyExpenses, seed.CustomerCount))
                })
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
}
