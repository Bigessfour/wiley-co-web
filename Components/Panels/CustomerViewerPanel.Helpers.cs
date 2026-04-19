using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Panels;

public partial class CustomerViewerPanel
{
    private IReadOnlyList<string> GetCustomerServiceOptions()
        => WorkspaceState.CustomerServiceOptions;

    private IReadOnlyList<string> GetCustomerCityLimitOptions()
        => WorkspaceState.CustomerCityLimitOptions;

    private IReadOnlyList<UtilityCustomerRecord> GetFilteredCustomers()
        => BuildFilteredCustomers();

    private string GetSelectedCustomerServiceDisplay()
        => string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerService) ? "All Services" : WorkspaceState.SelectedCustomerService;

    private string GetSelectedCustomerCityLimitsDisplay()
        => string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerCityLimits) ? "All" : WorkspaceState.SelectedCustomerCityLimits;

    private string GetActiveCustomerCountDisplay()
        => allCustomers.Count(customer => string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);

    private string GetLiveFilteredCustomerCountDisplay()
        => GetFilteredCustomers().Count.ToString(CultureInfo.InvariantCulture);

    private string GetCustomerDirectoryStatus()
        => customerDirectoryStatus;

    private bool IsBusy()
        => isLoadingCustomers || isSavingCustomer || isDeletingCustomer;

    private bool HasApiFeedback()
        => !string.IsNullOrWhiteSpace(GetCustomerApiError());

    private string GetCustomerApiError()
        => customerApiError ?? string.Empty;

    private IReadOnlyList<string> GetValidationSummaryItems()
        => BuildValidationSummaryItems();

    private bool IsEditorDialogOpen()
        => isEditorDialogOpen;

    private bool IsDeleteDialogOpen()
        => isDeleteDialogOpen;

    private UtilityCustomerRecord? GetPendingDeleteCustomer()
        => pendingDeleteCustomer;

    private EditContext? GetEditorEditContext()
        => editorEditContext;

    private string GetEditorDialogTitle()
        => isEditingExistingCustomer ? "Edit Utility Customer" : "Add Utility Customer";

    private string GetEditorSaveButtonText()
        => isEditingExistingCustomer ? "Save changes" : "Create customer";

    private IReadOnlyList<UtilityCustomerRecord> BuildFilteredCustomers()
    {
        return [
            .. allCustomers.Where(customer =>
                MatchesCustomerSearchTerm(customer)
                && MatchesCustomerServiceFilter(customer)
                && MatchesCustomerCityLimitsFilter(customer))
        ];
    }

    private bool MatchesCustomerSearchTerm(UtilityCustomerRecord customer)
    {
        var searchTerm = WorkspaceState.CustomerSearchTerm;
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        return MatchesAnyCustomerSearchField(customer, searchTerm);
    }

    private static bool ContainsCustomerSearchTerm(string value, string searchTerm)
        => value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesAnyCustomerSearchField(UtilityCustomerRecord customer, string searchTerm)
        => new[]
        {
            customer.DisplayName,
            customer.AccountNumber,
            customer.ServiceCity,
            customer.CustomerType,
            customer.ServiceLocation
        }.Any(value => ContainsCustomerSearchTerm(value, searchTerm));

    private bool MatchesCustomerServiceFilter(UtilityCustomerRecord customer)
    {
        return string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerService)
            || string.Equals(WorkspaceState.SelectedCustomerService, "All Services", StringComparison.Ordinal)
            || string.Equals(customer.CustomerType, WorkspaceState.SelectedCustomerService, StringComparison.Ordinal);
    }

    private bool MatchesCustomerCityLimitsFilter(UtilityCustomerRecord customer)
    {
        return string.IsNullOrWhiteSpace(WorkspaceState.SelectedCustomerCityLimits)
            || string.Equals(WorkspaceState.SelectedCustomerCityLimits, "All", StringComparison.Ordinal)
            || string.Equals(ToCityLimitsFlag(customer), WorkspaceState.SelectedCustomerCityLimits, StringComparison.Ordinal);
    }

    private string BuildActiveCustomerCountDisplay()
        => allCustomers.Count(customer => string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);

    private string BuildLiveFilteredCustomerCountDisplay()
        => GetFilteredCustomers().Count.ToString(CultureInfo.InvariantCulture);

    private IReadOnlyList<string> BuildValidationSummaryItems()
        => [.. customerValidationErrors.Values.SelectMany(messages => messages).Distinct(StringComparer.Ordinal)];
}