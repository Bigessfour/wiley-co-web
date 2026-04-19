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
        new Dictionary<string, EnterpriseBaselineSeed>(StringComparer.OrdinalIgnoreCase)
        {
            ["Water Utility"] = new(31.25m, 98000m, 4500),
            ["Wiley Sanitation District"] = new(21.50m, 72000m, 3200),
            ["Sanitation Utility"] = new(21.50m, 72000m, 3200)
        };
}
