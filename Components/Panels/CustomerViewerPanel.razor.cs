using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Navigations;
using Action = System.Action;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Panels;

public partial class CustomerViewerPanel : ComponentBase
{
    private static IReadOnlyList<string> CustomerDirectoryToolbarItems { get; } = ["ExcelExport"];

    [Inject] private UtilityCustomerApiService UtilityCustomerApiService { get; set; } = default!;
    [Inject] private WorkspaceState WorkspaceState { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

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
    private bool isExportingCustomerGrid;
    private bool isEditorDialogOpen;
    private bool isDeleteDialogOpen;
    private bool isEditingExistingCustomer;
    private SfGrid<UtilityCustomerRecord> CustomerDirectoryGrid = default!;

    protected override Task OnInitializedAsync()
    {
        return LoadCustomersAsync();
    }

    private async Task RefreshCustomerDirectoryAsync()
    {
        await LoadCustomersAsync("Refreshed the live utility-customer directory.");
    }

    private async Task HandleToolbarClickAsync(ClickEventArgs args)
    {
        if (!string.Equals(args.Item?.Id, "customer-directory-grid_excelexport", StringComparison.Ordinal))
        {
            return;
        }

        await ExportCustomerDirectoryAsync();
    }

    private async Task ExportCustomerDirectoryAsync()
    {
        if (isExportingCustomerGrid || isLoadingCustomers)
        {
            return;
        }

        isExportingCustomerGrid = true;
        customerDirectoryStatus = "Preparing the filtered customer directory Excel export...";
        await InvokeAsync(StateHasChanged);

        try
        {
            await CustomerDirectoryGrid.ExportToExcelAsync(new ExcelExportProperties());
            customerDirectoryStatus = "Customer directory Excel export generated.";
            ToastService.ShowSuccess("Customer export started", "The filtered customer directory is being downloaded as an Excel workbook.");
        }
        catch (Exception ex)
        {
            customerApiError = $"Unable to export the customer directory: {ex.Message}";
            customerDirectoryStatus = "Customer directory export failed.";
            ToastService.ShowError("Customer export failed", customerApiError);
        }
        finally
        {
            isExportingCustomerGrid = false;
            await InvokeAsync(StateHasChanged);
        }
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
        isDeleteDialogOpen = false;
        pendingDeleteCustomer = null;
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

    private void CloseDeleteDialog()
    {
        if (isDeletingCustomer)
        {
            return;
        }

        isDeleteDialogOpen = false;
        pendingDeleteCustomer = null;
    }

    private void BeginDeleteCustomer(UtilityCustomerRecord customer)
    {
        ResetApiFeedback();
        pendingDeleteCustomer = customer;
        isDeleteDialogOpen = true;
    }

    private async Task SaveCustomerAsync()
    {
        if (!TryPrepareCustomerSave(out var saveContext))
        {
            return;
        }

        await ExecuteCustomerOperationAsync(
            BeginSavingCustomer,
            () => SaveCustomerCoreAsync(saveContext),
            HandleSaveCustomerFailure,
            EndSavingCustomer);
    }

    private bool TryPrepareCustomerSave(out CustomerSaveContext saveContext)
    {
        saveContext = default!;

        if (!CanSaveCustomer())
        {
            return false;
        }

        saveContext = BuildCustomerSaveContext();
        return true;
    }

    private bool CanSaveCustomer()
    {
        var editContext = editorEditContext;
        if (isSavingCustomer || editContext is null)
        {
            return false;
        }

        return ValidateCustomerSaveContext(editContext);
    }

    private bool ValidateCustomerSaveContext(EditContext editContext)
    {
        ResetApiFeedback();
        if (!editContext.Validate())
        {
            customerApiError = "Fix the validation issues before saving this customer.";
            return false;
        }

        return true;
    }

    private CustomerSaveContext BuildCustomerSaveContext()
    {
        return new CustomerSaveContext(
            editorModel.ToRequest(),
            ResolveCustomerSaveId(),
            editorModel.AccountNumber,
            ResolveCustomerSaveActionLabel());
    }

    private int? ResolveCustomerSaveId()
    {
        return isEditingExistingCustomer && editorModel.Id > 0 ? editorModel.Id : null;
    }

    private string ResolveCustomerSaveActionLabel()
    {
        return isEditingExistingCustomer ? "updated" : "created";
    }

    private async Task DeleteCustomerAsync()
    {
        if (isDeletingCustomer || pendingDeleteCustomer is null)
        {
            return;
        }

        ResetApiFeedback();

        var customer = pendingDeleteCustomer;
        await ExecuteCustomerOperationAsync(
            BeginDeletingCustomer,
            () => DeleteCustomerCoreAsync(customer),
            HandleDeleteCustomerFailure,
            EndDeletingCustomer);
    }

    private async Task LoadCustomersAsync(string? successMessage = null)
    {
        ResetApiFeedback();

        await ExecuteCustomerOperationAsync(
            BeginLoadingCustomers,
            () => LoadCustomersCoreAsync(successMessage),
            HandleLoadCustomersFailure,
            EndLoadingCustomers);
    }

    private async Task ExecuteCustomerOperationAsync(Action begin, Func<Task> operation, Action<Exception> handleFailure, Action complete)
    {
        begin();
        StateHasChanged();

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            handleFailure(ex);
        }
        finally
        {
            complete();
            StateHasChanged();
        }
    }

    private void BeginSavingCustomer()
    {
        isSavingCustomer = true;
    }

    private void HandleSaveCustomerFailure(Exception ex)
    {
        if (ex is UtilityCustomerApiException apiException)
        {
            ApplyApiFeedback(apiException.Message, apiException.ValidationErrors);
            ToastService.ShowWarning("Customer save blocked", apiException.Message);
            return;
        }

        customerApiError = $"Unable to save the customer: {ex.Message}";
        ToastService.ShowError("Customer save failed", customerApiError);
    }

    private void EndSavingCustomer()
    {
        isSavingCustomer = false;
    }

    private async Task SaveCustomerCoreAsync(CustomerSaveContext saveContext)
    {
        if (saveContext.CustomerId.HasValue)
        {
            await UtilityCustomerApiService.UpdateCustomerAsync(saveContext.CustomerId.Value, saveContext.Request);
        }
        else
        {
            await UtilityCustomerApiService.CreateCustomerAsync(saveContext.Request);
        }

        isEditorDialogOpen = false;
        editorEditContext = null;
        await LoadCustomersAsync($"Saved {saveContext.AccountNumber} and {saveContext.ActionLabel} the live utility-customer directory.");
    }

    private void BeginDeletingCustomer()
    {
        isDeletingCustomer = true;
    }

    private void HandleDeleteCustomerFailure(Exception ex)
    {
        if (ex is UtilityCustomerApiException apiException)
        {
            ApplyApiFeedback(apiException.Message, apiException.ValidationErrors);
            ToastService.ShowWarning("Customer delete blocked", apiException.Message);
            return;
        }

        customerApiError = $"Unable to delete the customer: {ex.Message}";
        ToastService.ShowError("Customer delete failed", customerApiError);
    }

    private void EndDeletingCustomer()
    {
        isDeletingCustomer = false;
    }

    private async Task DeleteCustomerCoreAsync(UtilityCustomerRecord customer)
    {
        await UtilityCustomerApiService.DeleteCustomerAsync(customer.Id);
        isDeleteDialogOpen = false;
        pendingDeleteCustomer = null;
        await LoadCustomersAsync($"Deleted {customer.DisplayName} from the live utility-customer directory.");
    }

    private void BeginLoadingCustomers()
    {
        isLoadingCustomers = true;
        customerDirectoryStatus = "Loading live utility-customer directory...";
    }

    private void HandleLoadCustomersFailure(Exception ex)
    {
        customerApiError = $"Unable to load the live utility-customer directory: {ex.Message}";
        customerDirectoryStatus = "The live customer directory could not be refreshed.";
        ToastService.ShowError("Customer directory failed to load", customerApiError);
    }

    private void EndLoadingCustomers()
    {
        isLoadingCustomers = false;
    }

    private async Task LoadCustomersCoreAsync(string? successMessage)
    {
        var customers = await UtilityCustomerApiService.GetCustomersAsync();
        allCustomers.Clear();
        allCustomers.AddRange(customers.OrderBy(customer => customer.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(customer => customer.AccountNumber, StringComparer.OrdinalIgnoreCase));
        WorkspaceState.ReplaceCustomerDirectory([.. allCustomers.Select(ToCustomerRow)]);
        customerDirectoryStatus = successMessage ?? $"Loaded {allCustomers.Count} utility customers from the live API.";
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
}