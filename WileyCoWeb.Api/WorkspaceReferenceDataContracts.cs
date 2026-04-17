using WileyWidget.Models;

namespace WileyCoWeb.Contracts;

public sealed record WorkspaceReferenceDataImportRequest(
    string? ImportDataPath = null,
    bool IncludeSampleLedgerData = false,
    bool ApplyDefaultEnterpriseBaselines = false);

public sealed record WorkspaceReferenceDataImportResponse(
    string ImportDataPath,
    string ImportedAtUtc,
    int ImportedEnterpriseCount,
    int UpdatedEnterpriseCount,
    int DiscoveredCustomerReferenceRows,
    int ImportedUtilityCustomerCount,
    string UtilityCustomerImportStatus,
    IReadOnlyList<string> EnterpriseNames,
    int ImportedLedgerFileCount = 0,
    int ImportedLedgerRowCount = 0,
    string LedgerImportStatus = "Sample ledger import was not requested.",
    int SeededEnterpriseBaselineCount = 0);