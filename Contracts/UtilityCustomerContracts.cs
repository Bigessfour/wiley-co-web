namespace WileyCoWeb.Contracts;

public enum CustomerType
{
    Residential,
    Commercial,
    Industrial,
    Agricultural,
    Institutional,
    Government,
    MultiFamily
}

public enum ServiceLocation
{
    InsideCityLimits,
    OutsideCityLimits
}

public enum CustomerStatus
{
    Active,
    Inactive,
    Suspended,
    Closed
}

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