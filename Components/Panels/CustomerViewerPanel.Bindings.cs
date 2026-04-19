using Microsoft.AspNetCore.Components.Forms;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Components.Panels;

public partial class CustomerViewerPanel
{
    private UtilityCustomerEditorModel EditorModel
    {
        get
        {
            return editorModel;
        }
    }

    private string SearchTermProxy
    {
        get
        {
            return WorkspaceState.CustomerSearchTerm;
        }
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
        get
        {
            return WorkspaceState.SelectedCustomerService;
        }
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
        get
        {
            return WorkspaceState.SelectedCustomerCityLimits;
        }
        set
        {
            if (WorkspaceState.SelectedCustomerCityLimits == value)
            {
                return;
            }

            WorkspaceState.SetCustomerCityLimitsFilter(value);
        }
    }
}
