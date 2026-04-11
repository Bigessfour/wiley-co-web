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

		var bootstrap = WorkspaceTestData.CreateWaterUtilityBootstrap(
			WorkspaceTestData.CouncilReviewScenario,
			WorkspaceTestData.BaselineCurrentRate,
			WorkspaceTestData.BaselineTotalCosts,
			WorkspaceTestData.BaselineProjectedVolume,
			DateTime.UtcNow.ToString("O"),
			fiscalYearOptions: [WorkspaceTestData.PriorFiscalYear, WorkspaceTestData.WaterFiscalYear, 2027],
			scenarioItems: [
				new WorkspaceScenarioItemData(scenarioId, "Reserve transfer", 6200m)
			],
			customerRows: [
				new CustomerRow("North Plant", "Water", "Yes"),
				new CustomerRow("South Lift", "Sewer", "No")
			],
			projectionRows: [
				new ProjectionRow("FY26", WorkspaceTestData.BaselineCurrentRate),
				new ProjectionRow("FY27", 65.10m)
			]);

		state.ApplyBootstrap(bootstrap);
		var roundTripped = state.ToBootstrapData();

		Assert.Equal(WorkspaceTestData.WaterUtility, roundTripped.SelectedEnterprise);
		Assert.Equal(WorkspaceTestData.WaterFiscalYear, roundTripped.SelectedFiscalYear);
		Assert.Equal(WorkspaceTestData.CouncilReviewScenario, roundTripped.ActiveScenarioName);
		Assert.Equal(WorkspaceTestData.BaselineCurrentRate, roundTripped.CurrentRate);
		Assert.Equal(WorkspaceTestData.BaselineTotalCosts, roundTripped.TotalCosts);
		Assert.Equal(WorkspaceTestData.BaselineProjectedVolume, roundTripped.ProjectedVolume);
		Assert.Equal(new[] { WorkspaceTestData.WaterUtility, WorkspaceTestData.SanitationUtility }, roundTripped.EnterpriseOptions);
		Assert.Equal(new[] { WorkspaceTestData.PriorFiscalYear, WorkspaceTestData.WaterFiscalYear, 2027 }, roundTripped.FiscalYearOptions);
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
		state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
			WorkspaceTestData.BasePlanningScenario,
			WorkspaceTestData.WaterCurrentRate,
			WorkspaceTestData.WaterTotalCosts,
			WorkspaceTestData.WaterProjectedVolume,
			DateTime.UtcNow.ToString("O"),
			enterpriseOptions: [WorkspaceTestData.WaterUtility, WorkspaceTestData.TrashUtility],
			customerRows: [
				new CustomerRow("North Plant", "Water", "Yes"),
				new CustomerRow("South Lift", "Sewer", "No"),
				new CustomerRow("West Shop", "Water", "No")
			]));

		state.SetCustomerServiceFilter("Water");
		state.SetCustomerCityLimitsFilter("No");

		Assert.Equal(new[] { WorkspaceTestData.WaterUtility, WorkspaceTestData.TrashUtility }, state.EnterpriseOptions);
		Assert.Equal(new[] { WorkspaceTestData.PriorFiscalYear, WorkspaceTestData.WaterFiscalYear }, state.FiscalYearOptions);
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
