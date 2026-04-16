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

public sealed record UtilityCustomerUpsertRequest(
    string AccountNumber,
    string FirstName,
    string LastName,
    string? CompanyName,
    CustomerType CustomerType,
    string ServiceAddress,
    string ServiceCity,
    string ServiceState,
    string ServiceZipCode,
    ServiceLocation ServiceLocation,
    CustomerStatus Status,
    decimal CurrentBalance,
    DateTime? AccountOpenDate,
    string? PhoneNumber,
    string? EmailAddress,
    string? MeterNumber,
    string? Notes);

public sealed record UtilityCustomerRecord(
    int Id,
    string AccountNumber,
    string FirstName,
    string LastName,
    string? CompanyName,
    string DisplayName,
    string CustomerType,
    string ServiceAddress,
    string ServiceCity,
    string ServiceState,
    string ServiceZipCode,
    string ServiceLocation,
    string Status,
    decimal CurrentBalance,
    string AccountOpenDateUtc,
    string? PhoneNumber,
    string? EmailAddress,
    string? MeterNumber,
    string? Notes);