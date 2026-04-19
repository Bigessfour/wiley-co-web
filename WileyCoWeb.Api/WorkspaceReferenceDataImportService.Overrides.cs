namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{
    private static readonly (string Pattern, string Name)[] CompanyNameOverrides =
    [
        ("sanitation", "Wiley Sanitation District"),
        ("sewer", "Wiley Sanitation District"),
        ("wsd", "Wiley Sanitation District"),
        ("utility", "Water Utility"),
        ("tow utility account", "Water Utility"),
        ("town utility account", "Water Utility")
    ];

    private static readonly (string Pattern, string Name)[] EnterpriseNameOverrides =
    [
        ("WSD", "Wiley Sanitation District"),
        ("sanitation", "Wiley Sanitation District"),
        ("Util", "Water Utility"),
        ("utility", "Water Utility")
    ];

    private static readonly (string Pattern, string Code)[] EnterpriseCodeOverrides =
    [
        ("Sanitation", "WSD"),
        ("District", "WSD"),
        ("Water", "WTR"),
        ("Utility", "WTR")
    ];
}
