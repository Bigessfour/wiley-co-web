using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using WileyCoWeb.Components.Pages;

namespace WileyCoWeb.ComponentTests;

public sealed class WileyWorkspaceBaseHarness : WileyWorkspaceBase
{
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		builder.OpenElement(0, "div");

		builder.OpenElement(1, "button");
		builder.AddAttribute(2, "id", "save-baseline-button");
		builder.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, SaveWorkspaceBaselineAsync));
		builder.AddContent(4, "Save workspace baseline");
		builder.CloseElement();

		builder.OpenElement(5, "button");
		builder.AddAttribute(6, "id", "save-scenario-button");
		builder.AddAttribute(7, "onclick", EventCallback.Factory.Create(this, SaveScenarioAsync));
		builder.AddContent(8, "Save scenario");
		builder.CloseElement();

		builder.OpenElement(9, "button");
		builder.AddAttribute(10, "id", "apply-scenario-button");
		builder.AddAttribute(11, "disabled", !CanApplySelectedScenario);
		builder.AddAttribute(12, "onclick", EventCallback.Factory.Create(this, ApplySelectedScenarioAsync));
		builder.AddContent(13, "Apply saved scenario");
		builder.CloseElement();

		builder.OpenElement(14, "span");
		builder.AddAttribute(15, "id", "baseline-status");
		builder.AddContent(16, BaselineSaveStatus);
		builder.CloseElement();

		builder.OpenElement(17, "span");
		builder.AddAttribute(18, "id", "scenario-status");
		builder.AddContent(19, ScenarioPersistenceStatus);
		builder.CloseElement();

		builder.OpenElement(20, "span");
		builder.AddAttribute(21, "id", "workspace-status");
		builder.AddContent(22, WorkspaceLoadStatus);
		builder.CloseElement();

		builder.CloseElement();
	}
}
