using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Components.Panels;

public partial class CustomerViewerPanel
{
    private static IReadOnlyList<EnumOption<CustomerType>> CustomerTypeOptions { get; } =
    [
        new(CustomerType.Residential, "Residential"),
        new(CustomerType.Commercial, "Commercial"),
        new(CustomerType.Industrial, "Industrial"),
        new(CustomerType.Agricultural, "Agricultural"),
        new(CustomerType.Institutional, "Institutional"),
        new(CustomerType.Government, "Government"),
        new(CustomerType.MultiFamily, "Multi-Family")
    ];

    private static IReadOnlyList<EnumOption<ServiceLocation>> ServiceLocationOptions { get; } =
    [
        new(ServiceLocation.InsideCityLimits, "Inside City Limits"),
        new(ServiceLocation.OutsideCityLimits, "Outside City Limits")
    ];

    private static IReadOnlyList<EnumOption<CustomerStatus>> CustomerStatusOptions { get; } =
    [
        new(CustomerStatus.Active, "Active"),
        new(CustomerStatus.Inactive, "Inactive"),
        new(CustomerStatus.Suspended, "Suspended"),
        new(CustomerStatus.Closed, "Closed")
    ];

    private sealed record CustomerSaveContext(
        UtilityCustomerUpsertRequest Request,
        int? CustomerId,
        string AccountNumber,
        string ActionLabel);

    private static CustomerRow ToCustomerRow(UtilityCustomerRecord customer)
        => new(customer.DisplayName, customer.CustomerType, ToCityLimitsFlag(customer));

    private static string ToCityLimitsFlag(UtilityCustomerRecord customer)
        => ParseServiceLocation(customer.ServiceLocation) == ServiceLocation.InsideCityLimits ? "Yes" : "No";

    private static CustomerType ParseCustomerType(string value)
        => value.Trim() switch
        {
            "Residential" => CustomerType.Residential,
            "Commercial" => CustomerType.Commercial,
            "Industrial" => CustomerType.Industrial,
            "Agricultural" => CustomerType.Agricultural,
            "Institutional" => CustomerType.Institutional,
            "Government" => CustomerType.Government,
            "Multi-Family" => CustomerType.MultiFamily,
            _ => CustomerType.Residential
        };

    private static ServiceLocation ParseServiceLocation(string value)
        => value.Trim() switch
        {
            "Inside City Limits" => ServiceLocation.InsideCityLimits,
            "Outside City Limits" => ServiceLocation.OutsideCityLimits,
            _ => ServiceLocation.InsideCityLimits
        };

    private static CustomerStatus ParseCustomerStatus(string value)
        => value.Trim() switch
        {
            "Inactive" => CustomerStatus.Inactive,
            "Suspended" => CustomerStatus.Suspended,
            "Closed" => CustomerStatus.Closed,
            _ => CustomerStatus.Active
        };

    private sealed record EnumOption<TValue>(TValue Value, string Text);

    private sealed class UtilityCustomerEditorModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Account number is required")]
        [StringLength(20, ErrorMessage = "Account number cannot exceed 20 characters")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters")]
        public string? CompanyName { get; set; }

        public CustomerType CustomerType { get; set; } = CustomerType.Residential;

        [Required(ErrorMessage = "Service address is required")]
        [StringLength(200, ErrorMessage = "Service address cannot exceed 200 characters")]
        public string ServiceAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Service city is required")]
        [StringLength(50, ErrorMessage = "Service city cannot exceed 50 characters")]
        public string ServiceCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "Service state is required")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Service state must be exactly 2 characters")]
        public string ServiceState { get; set; } = "CO";

        [Required(ErrorMessage = "Service ZIP code is required")]
        [StringLength(10, ErrorMessage = "Service ZIP code cannot exceed 10 characters")]
        public string ServiceZipCode { get; set; } = string.Empty;

        public ServiceLocation ServiceLocation { get; set; } = ServiceLocation.InsideCityLimits;

        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        public decimal CurrentBalance { get; set; }

        public DateTime? AccountOpenDate { get; set; } = DateTime.Today;

        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? PhoneNumber { get; set; }

        [EmailAddress(ErrorMessage = "Email address must be valid")]
        [StringLength(100, ErrorMessage = "Email address cannot exceed 100 characters")]
        public string? EmailAddress { get; set; }

        [StringLength(20, ErrorMessage = "Meter number cannot exceed 20 characters")]
        public string? MeterNumber { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }

        public UtilityCustomerUpsertRequest ToRequest()
            => new(
                AccountNumber.Trim(),
                FirstName.Trim(),
                LastName.Trim(),
                NormalizeOptional(CompanyName),
                CustomerType,
                ServiceAddress.Trim(),
                ServiceCity.Trim(),
                ServiceState.Trim().ToUpperInvariant(),
                ServiceZipCode.Trim(),
                ServiceLocation,
                Status,
                CurrentBalance,
                AccountOpenDate,
                NormalizeOptional(PhoneNumber),
                NormalizeOptional(EmailAddress),
                NormalizeOptional(MeterNumber),
                NormalizeOptional(Notes));

        public static UtilityCustomerEditorModel CreateDefault()
            => new()
            {
                AccountOpenDate = DateTime.Today,
                ServiceState = "CO",
                Status = CustomerStatus.Active,
                CustomerType = CustomerType.Residential,
                ServiceLocation = ServiceLocation.InsideCityLimits
            };

        public static UtilityCustomerEditorModel FromRecord(UtilityCustomerRecord customer)
            => new()
            {
                Id = customer.Id,
                AccountNumber = customer.AccountNumber,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                CompanyName = customer.CompanyName,
                CustomerType = ParseCustomerType(customer.CustomerType),
                ServiceAddress = customer.ServiceAddress,
                ServiceCity = customer.ServiceCity,
                ServiceState = customer.ServiceState,
                ServiceZipCode = customer.ServiceZipCode,
                ServiceLocation = ParseServiceLocation(customer.ServiceLocation),
                Status = ParseCustomerStatus(customer.Status),
                CurrentBalance = customer.CurrentBalance,
                AccountOpenDate = DateTime.TryParse(customer.AccountOpenDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var openedAt)
                    ? openedAt.Date
                    : DateTime.Today,
                PhoneNumber = customer.PhoneNumber,
                EmailAddress = customer.EmailAddress,
                MeterNumber = customer.MeterNumber,
                Notes = customer.Notes
            };

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
