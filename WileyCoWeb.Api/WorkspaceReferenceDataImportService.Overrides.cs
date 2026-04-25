using WileyWidget.Models;

namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{
    private static readonly (string Pattern, string Name)[] CompanyNameOverrides =
    [
        ("sanitation", "Wiley Sanitation District"),
        ("sewer", "Wiley Sanitation District"),
        ("wsd", "Wiley Sanitation District"),
        ("utility", WorkspaceEnterpriseCatalog.WaterUtility),
        ("tow utility account", WorkspaceEnterpriseCatalog.WaterUtility),
        ("town utility account", WorkspaceEnterpriseCatalog.WaterUtility)
    ];

    private static readonly (string Pattern, string Name)[] EnterpriseNameOverrides =
    [
        ("WSD", "Wiley Sanitation District"),
        ("sanitation", "Wiley Sanitation District"),
        ("Util", WorkspaceEnterpriseCatalog.WaterUtility),
        ("utility", WorkspaceEnterpriseCatalog.WaterUtility)
    ];

    private static readonly (string Pattern, string Code)[] EnterpriseCodeOverrides =
    [
        ("Sanitation", "WSD"),
        ("District", "WSD"),
        ("Water", "WTR"),
        ("Utility", "WTR")
    ];
}
