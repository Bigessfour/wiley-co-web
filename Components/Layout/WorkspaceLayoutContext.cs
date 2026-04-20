namespace WileyCoWeb.Components.Layout;

public sealed class WorkspaceLayoutContext
{
    public bool IsLeftNavCollapsed { get; set; }

    public Func<Task> ToggleLeftNavAsync { get; set; } = static () => Task.CompletedTask;
}