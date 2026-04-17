using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Panels;

public partial class CustomerViewerPanel : ComponentBase
{
    [Inject] private UtilityCustomerApiService UtilityCustomerApiService { get; set; } = default!;
    [Inject] private WorkspaceState WorkspaceState { get; set; } = default!;

    private readonly List<UtilityCustomerRecord> allCustomers = [];
    private readonly Dictionary<string, string[]> customerValidationErrors = new(StringComparer.OrdinalIgnoreCase);
    private UtilityCustomerEditorModel editorModel = UtilityCustomerEditorModel.CreateDefault();
    private EditContext? editorEditContext;
    private UtilityCustomerRecord? pendingDeleteCustomer;
    private string customerDirectoryStatus = "Loading live utility-customer directory...";
    private string? customerApiError;
    private bool isLoadingCustomers = true;
    private bool isSavingCustomer;
    private bool isDeletingCustomer;
    private bool isEditorDialogOpen;
    private bool isDeleteDialogOpen;
    private bool isEditingExistingCustomer;

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

    private string SearchTermProxy
    {
        get => WorkspaceState.CustomerSearchTerm;
        set
        {
            if (WorkspaceState.CustomerSearchTerm == value)
            {
                return;
            }

            WorkspaceState.SetCustomerSearchTerm(value);
        }
    }

    private string SelectedCustomerServiceProxy
    {
        get => WorkspaceState.SelectedCustomerService;
        set
        {
            if (WorkspaceState.SelectedCustomerService == value)
            {
                return;
            }

            WorkspaceState.SetCustomerServiceFilter(value);
        }
    }

    private string SelectedCustomerCityLimitsProxy
    {
        get => WorkspaceState.SelectedCustomerCityLimits;
        set
        {
            if (WorkspaceState.SelectedCustomerCityLimits == value)
            {
                return;
            }

            WorkspaceState.SetCustomerCityLimitsFilter(value);
        }
    }

    private IReadOnlyList<string> CustomerServiceOptions => WorkspaceState.CustomerServiceOptions;

    private IReadOnlyList<string> CustomerCityLimitOptions => WorkspaceState.CustomerCityLimitOptions;

    private IReadOnlyList<UtilityCustomerRecord> FilteredCustomers =>
    [
        .. allCustomers.Where(customer =>
            (string.IsNullOrWhiteSpace(WorkspaceState.CustomerSearchTerm)
             || customer.DisplayName.Contains(WorkspaceState.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase)
             || customer.AccountNumber.Contains(WorkspaceState.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase)
             || customer.ServiceCity.Contains(WorkspaceState.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase)
             || customer.CustomerType.Contains(WorkspaceState.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase)
             || customer.ServiceLocation.Contains(WorkspaceState.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerService)
                || string.Equals(WorkspaceState.SelectedCustomerService, "All Services", StringComparison.Ordinal)
                || string.Equals(customer.CustomerType, WorkspaceState.SelectedCustomerService, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerCityLimits)
                || string.Equals(WorkspaceState.SelectedCustomerCityLimits, "All", StringComparison.Ordinal)
                || string.Equals(ToCityLimitsFlag(customer), WorkspaceState.SelectedCustomerCityLimits, StringComparison.Ordinal)))
    ];

    private string SelectedCustomerServiceDisplay => string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerService) ? "All Services" : WorkspaceState.SelectedCustomerService;
    private string SelectedCustomerCityLimitsDisplay => string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerCityLimits) ? "All" : WorkspaceState.SelectedCustomerCityLimits;
    private string ActiveCustomerCountDisplay => allCustomers.Count(customer => string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);
    private string LiveFilteredCustomerCountDisplay => FilteredCustomers.Count.ToString(CultureInfo.InvariantCulture);
    private string CustomerDirectoryStatus => customerDirectoryStatus;
    private bool IsBusy => isLoadingCustomers || isSavingCustomer || isDeletingCustomer;
    private bool HasApiFeedback => !string.IsNullOrWhiteSpace(CustomerApiError);
    private string CustomerApiError => customerApiError ?? string.Empty;
    private IReadOnlyList<string> ValidationSummaryItems =>
    [
        .. customerValidationErrors.Values.SelectMany(messages => messages).Distinct(StringComparer.Ordinal)
    ];
    private bool IsEditorDialogOpen => isEditorDialogOpen;
    private bool IsDeleteDialogOpen => isDeleteDialogOpen;
    private UtilityCustomerRecord? PendingDeleteCustomer => pendingDeleteCustomer;
    private EditContext? EditorEditContext => editorEditContext;
    private UtilityCustomerEditorModel EditorModel => editorModel;
    private string EditorDialogTitle => isEditingExistingCustomer ? "Edit Utility Customer" : "Add Utility Customer";
    private string EditorSaveButtonText => isEditingExistingCustomer ? "Save changes" : "Create customer";

    protected override async Task OnInitializedAsync()
    {
        await LoadCustomersAsync();
    }

    private async Task RefreshCustomerDirectoryAsync()
    {
        await LoadCustomersAsync("Refreshed the live utility-customer directory.");
    }

    private async Task ClearFiltersAsync()
    {
        WorkspaceState.ClearCustomerFilters();
        await InvokeAsync(StateHasChanged);
    }

    private void BeginCreateCustomer()
    {
        ResetApiFeedback();
        isEditingExistingCustomer = false;
        editorModel = UtilityCustomerEditorModel.CreateDefault();
        editorEditContext = new EditContext(editorModel);
        isEditorDialogOpen = true;
    }

    private void BeginEditCustomer(UtilityCustomerRecord customer)
    {
        ResetApiFeedback();
        isEditingExistingCustomer = true;
        editorModel = UtilityCustomerEditorModel.FromRecord(customer);
        editorEditContext = new EditContext(editorModel);
        isEditorDialogOpen = true;
    }

    private void CloseEditorDialog()
    {
        if (isSavingCustomer)
        {
            return;
        }

        isEditorDialogOpen = false;
        editorEditContext = null;
    }

    private void BeginDeleteCustomer(UtilityCustomerRecord customer)
    {
        ResetApiFeedback();
        pendingDeleteCustomer = customer;
        isDeleteDialogOpen = true;
    }

    private void CloseDeleteDialog()
    {
        if (isDeletingCustomer)
        {
            return;
        }

        isDeleteDialogOpen = false;
        pendingDeleteCustomer = null;
    }

    private async Task SaveCustomerAsync()
    {
        if (isSavingCustomer || editorEditContext is null)
        {
            return;
        }

        ResetApiFeedback();
        if (!editorEditContext.Validate())
        {
            customerApiError = "Fix the validation issues before saving this customer.";
            return;
        }

        isSavingCustomer = true;

        try
        {
            var request = editorModel.ToRequest();
            var actionLabel = isEditingExistingCustomer ? "updated" : "created";

            if (isEditingExistingCustomer && editorModel.Id > 0)
            {
                await UtilityCustomerApiService.UpdateCustomerAsync(editorModel.Id, request);
            }
            else
            {
                await UtilityCustomerApiService.CreateCustomerAsync(request);
            }

            isEditorDialogOpen = false;
            editorEditContext = null;
            await LoadCustomersAsync($"Saved {editorModel.AccountNumber} and {actionLabel} the live utility-customer directory.");
        }
        catch (UtilityCustomerApiException ex)
        {
            ApplyApiFeedback(ex.Message, ex.ValidationErrors);
        }
        catch (Exception ex)
        {
            customerApiError = $"Unable to save the customer: {ex.Message}";
        }
        finally
        {
            isSavingCustomer = false;
        }
    }

    private async Task DeleteCustomerAsync()
    {
        if (isDeletingCustomer || pendingDeleteCustomer is null)
        {
            return;
        }

        ResetApiFeedback();
        isDeletingCustomer = true;

        try
        {
            var deletedCustomerName = pendingDeleteCustomer.DisplayName;
            await UtilityCustomerApiService.DeleteCustomerAsync(pendingDeleteCustomer.Id);
            isDeleteDialogOpen = false;
            pendingDeleteCustomer = null;
            await LoadCustomersAsync($"Deleted {deletedCustomerName} from the live utility-customer directory.");
        }
        catch (UtilityCustomerApiException ex)
        {
            ApplyApiFeedback(ex.Message, ex.ValidationErrors);
        }
        catch (Exception ex)
        {
            customerApiError = $"Unable to delete the customer: {ex.Message}";
        }
        finally
        {
            isDeletingCustomer = false;
        }
    }

    private async Task LoadCustomersAsync(string? successMessage = null)
    {
        ResetApiFeedback();
        isLoadingCustomers = true;
        customerDirectoryStatus = "Loading live utility-customer directory...";
        StateHasChanged();

        try
        {
            var customers = await UtilityCustomerApiService.GetCustomersAsync();
            allCustomers.Clear();
            allCustomers.AddRange(customers.OrderBy(customer => customer.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(customer => customer.AccountNumber, StringComparer.OrdinalIgnoreCase));
            WorkspaceState.ReplaceCustomerDirectory([.. allCustomers.Select(ToCustomerRow)]);
            customerDirectoryStatus = successMessage ?? $"Loaded {allCustomers.Count} utility customers from the live API.";
        }
        catch (Exception ex)
        {
            customerApiError = $"Unable to load the live utility-customer directory: {ex.Message}";
            customerDirectoryStatus = "The live customer directory could not be refreshed.";
        }
        finally
        {
            isLoadingCustomers = false;
            StateHasChanged();
        }
    }

    private void ResetApiFeedback()
    {
        customerApiError = null;
        customerValidationErrors.Clear();
    }

    private void ApplyApiFeedback(string message, IReadOnlyDictionary<string, string[]> validationErrors)
    {
        customerApiError = message;
        customerValidationErrors.Clear();

        foreach (var validationError in validationErrors)
        {
            customerValidationErrors[validationError.Key] = validationError.Value;
        }
    }

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