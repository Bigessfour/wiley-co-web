using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceStateTests
{
	[Fact]
	public void ApplyBootstrap_ThenToBootstrapData_RoundTripsSharedWorkspaceContract()
	{
		var state = new WorkspaceState();
		var scenarioId = Guid.NewGuid();

		var bootstrap = new WorkspaceBootstrapData(
			"Water Utility",
			2026,
			"Council Review Scenario",
			61.75m,
			15500m,
			275m,
			DateTime.UtcNow.ToString("O"))
		{
			EnterpriseOptions = ["Water Utility", "Sanitation Utility"],
			FiscalYearOptions = [2025, 2026, 2027],
			CustomerServiceOptions = ["All Services", "Water", "Sewer"],
			CustomerCityLimitOptions = ["All", "Yes", "No"],
			ScenarioItems =
			[
				new WorkspaceScenarioItemData(scenarioId, "Reserve transfer", 6200m)
			],
			CustomerRows =
			[
				new CustomerRow("North Plant", "Water", "Yes"),
				new CustomerRow("South Lift", "Sewer", "No")
			],
			ProjectionRows =
			[
				new ProjectionRow("FY26", 61.75m),
				new ProjectionRow("FY27", 65.10m)
			]
		};

		state.ApplyBootstrap(bootstrap);
		var roundTripped = state.ToBootstrapData();

		Assert.Equal("Water Utility", roundTripped.SelectedEnterprise);
		Assert.Equal(2026, roundTripped.SelectedFiscalYear);
		Assert.Equal("Council Review Scenario", roundTripped.ActiveScenarioName);
		Assert.Equal(61.75m, roundTripped.CurrentRate);
		Assert.Equal(15500m, roundTripped.TotalCosts);
		Assert.Equal(275m, roundTripped.ProjectedVolume);
		Assert.Equal(new[] { "Water Utility", "Sanitation Utility" }, roundTripped.EnterpriseOptions);
		Assert.Equal(new[] { 2025, 2026, 2027 }, roundTripped.FiscalYearOptions);
		Assert.Equal(new[] { "All Services", "Water", "Sewer" }, roundTripped.CustomerServiceOptions);
		Assert.Single(roundTripped.ScenarioItems!);
		Assert.Equal(scenarioId, roundTripped.ScenarioItems![0].Id);
		Assert.Equal(2, roundTripped.CustomerRows?.Count);
		Assert.Equal(2, roundTripped.ProjectionRows?.Count);
	}

	[Fact]
	public void ApplyBootstrap_UsesSharedContractOptionsForFilteringAndSelection()
	{
		var state = new WorkspaceState();
		state.ApplyBootstrap(new WorkspaceBootstrapData(
			"Water Utility",
			2026,
			"Base Planning Scenario",
			55.25m,
			13250m,
			240m,
			DateTime.UtcNow.ToString("O"))
		{
			EnterpriseOptions = ["Water Utility", "Trash Utility"],
			FiscalYearOptions = [2025, 2026],
			CustomerRows =
			[
				new CustomerRow("North Plant", "Water", "Yes"),
				new CustomerRow("South Lift", "Sewer", "No"),
				new CustomerRow("West Shop", "Water", "No")
			]
		});

		state.SetCustomerServiceFilter("Water");
		state.SetCustomerCityLimitsFilter("No");

		Assert.Equal(new[] { "Water Utility", "Trash Utility" }, state.EnterpriseOptions);
		Assert.Equal(new[] { 2025, 2026 }, state.FiscalYearOptions);
		Assert.Single(state.FilteredCustomers);
		Assert.Equal("West Shop", state.FilteredCustomers[0].Name);
	}

	[Fact]
	public void SetCurrentStateSource_TracksBrowserRestoredStateWithoutChangingStartupSource()
	{
		var state = new WorkspaceState();

		state.SetStartupSource(WorkspaceStartupSource.ApiSnapshot, "Workspace started from the live workspace API snapshot.");
		state.SetCurrentStateSource(WorkspaceStartupSource.BrowserStorageRestore, "Current workspace state was restored from browser storage.");

		Assert.Equal(WorkspaceStartupSource.ApiSnapshot, state.StartupSource);
		Assert.Equal(WorkspaceStartupSource.BrowserStorageRestore, state.CurrentStateSource);
		Assert.True(state.IsUsingBrowserRestoredState);
		Assert.Contains("browser storage", state.CurrentStateSourceStatus);
		Assert.Contains("live workspace API snapshot", state.StartupSourceStatus);
	}

	[Fact]
	public void SetStartupSource_UpdatesStartupStatusAndFallbackFlag()
	{
		var state = new WorkspaceState();

		state.SetStartupSource(WorkspaceStartupSource.LocalBootstrapFallback, "Workspace started from local fallback data because the workspace API was unavailable.");

		Assert.Equal(WorkspaceStartupSource.LocalBootstrapFallback, state.StartupSource);
		Assert.True(state.IsUsingStartupFallback);
		Assert.Contains("local fallback data", state.StartupSourceStatus);
	}
}
