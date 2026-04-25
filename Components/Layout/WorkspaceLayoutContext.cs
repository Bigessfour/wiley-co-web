namespace WileyCoWeb.Components.Layout;

// ─────────────────────────────────────────────────────────────────────────────
// WorkspaceLayoutContext — centralized shell layout state
//
// PATTERN
//   MainLayout creates one instance, wraps the entire component tree in a
//   <CascadingValue Value="_layoutContext">, and owns all mutation.  Child
//   components (WileyWorkspaceBase, panels) read state via
//   [CascadingParameter] and subscribe to OnChange for reactive updates.
//
// THREAD-SAFETY
//   Blazor WASM runs on a single JS thread; no locking is required.
//   Blazor Server callers should marshal mutations through InvokeAsync before
//   calling any Set* method.
//
// PERSISTENCE
//   MainLayout is responsible for reading/writing localStorage via JS interop.
//   The context itself is pure in-memory state; it knows nothing about storage.
//
// DISPOSAL
//   MainLayout calls Dispose() when it tears down (rare in WASM, but correct
//   for Blazor Server circuits).  Disposal clears OnChange to prevent leaked
//   subscriber references.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Viewport-size breakpoints used by the workspace shell.</summary>
public enum WorkspaceLayoutMode
{
    /// <summary>Full desktop — sidebar rail expanded by default, all panes visible.</summary>
    Desktop,

    /// <summary>Tablet — sidebar rail docked, context rail may auto-collapse.</summary>
    Tablet,

    /// <summary>Mobile — sidebar hidden, single-pane stacking.</summary>
    Mobile
}

/// <summary>
/// Centralized layout state for the Wiley workspace shell.  Cascaded from
/// <c>MainLayout</c> so every child panel can react to layout changes without
/// prop-drilling.  All mutations go through the <c>Set*</c> methods, which
/// fire <see cref="OnChange"/> so subscribers can call
/// <c>StateHasChanged()</c> on their own schedule.
/// </summary>
public sealed class WorkspaceLayoutContext : IDisposable
{
    // ── Private backing fields ────────────────────────────────────────────────

    // Left navigation rail (SfSidebar in MainLayout).
    private bool _isLeftNavCollapsed;

    // Workspace splitter context-rail pane (pane 0 in WileyWorkspace.razor).
    private bool _isContextRailCollapsed;

    // Jarvis AI chat panel (pane 2 in the workspace splitter).
    private bool _isJarvisOpen;

    // Viewport-width-derived layout mode, updated by JS resize observer.
    private WorkspaceLayoutMode _layoutMode = WorkspaceLayoutMode.Desktop;

    // Right-to-left text direction, detected from document.documentElement.dir.
    private bool _enableRtl;

    // ── Change notification ───────────────────────────────────────────────────

    /// <summary>
    /// Fires whenever any layout property changes.  Subscribers should call
    /// <c>InvokeAsync(StateHasChanged)</c> inside the handler to re-render safely.
    /// </summary>
    public event Action? OnChange;

    // ── Public read-only properties ───────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when the left navigation rail (SfSidebar) is in its
    /// docked/collapsed state.  Persisted in localStorage.
    /// </summary>
    public bool IsLeftNavCollapsed => _isLeftNavCollapsed;

    /// <summary>
    /// <c>true</c> when the workspace context-rail splitter pane (pane 0) is
    /// collapsed.  Persisted in localStorage.
    /// </summary>
    public bool IsContextRailCollapsed => _isContextRailCollapsed;

    /// <summary>
    /// <c>true</c> when the Jarvis AI chat panel (pane 2) is expanded.
    /// Persisted in localStorage.
    /// </summary>
    public bool IsJarvisOpen => _isJarvisOpen;

    /// <summary>
    /// Viewport-derived layout mode, updated by the JS resize observer.
    /// Not persisted — recomputed from window width on every page load.
    /// </summary>
    public WorkspaceLayoutMode LayoutMode => _layoutMode;

    /// <summary>
    /// <c>true</c> when the host document is in RTL text direction
    /// (<c>document.documentElement.dir === "rtl"</c>).  Detected once during
    /// layout initialization and applied to all Syncfusion components that
    /// accept an <c>EnableRtl</c> parameter.
    /// </summary>
    public bool EnableRtl => _enableRtl;

    // ── Derived / computed helpers ────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when the viewport is narrower than a full desktop breakpoint
    /// (tablet or mobile).  Use this to adapt dense UI components.
    /// </summary>
    public bool IsCompactLayout => _layoutMode is WorkspaceLayoutMode.Tablet or WorkspaceLayoutMode.Mobile;

    /// <summary>
    /// CSS modifier class appended to the <c>.app-shell</c> root element.
    /// Empty string when the rail is expanded so no extra class is added.
    /// </summary>
    public string AppShellNavCssClass =>
        _isLeftNavCollapsed ? "app-shell-nav-collapsed" : string.Empty;

    /// <summary>
    /// Full CSS class string for the app-shell root <c>&lt;div&gt;</c>, combining
    /// the base class with the nav-collapsed modifier when appropriate.
    /// </summary>
    public string AppShellCssClass =>
        _isLeftNavCollapsed ? "app-shell app-shell-nav-collapsed" : "app-shell";

    // ── Delegates wired by MainLayout ─────────────────────────────────────────

    /// <summary>
    /// Triggers a full left-nav toggle (state mutation + JS storage persist).
    /// Wired by <c>MainLayout.OnInitialized</c>; never <c>null</c> at runtime.
    /// </summary>
    public Func<Task> ToggleLeftNavAsync { get; set; } = static () => Task.CompletedTask;

    // ── Mutation API ──────────────────────────────────────────────────────────
    // Called by the owning layout components (MainLayout, WileyWorkspace).
    // Public so both MainLayout (Components.Layout) and WileyWorkspaceBase
    // (Components.Pages) can call them without InternalsVisibleTo.

    /// <summary>
    /// Sets the left navigation rail collapsed state.
    /// No-op when the value is unchanged; fires <see cref="OnChange"/> otherwise.
    /// </summary>
    public void SetLeftNavCollapsed(bool collapsed)
    {
        if (_isLeftNavCollapsed == collapsed)
        {
            return;
        }

        _isLeftNavCollapsed = collapsed;
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the workspace context-rail splitter pane collapsed state.
    /// No-op when the value is unchanged; fires <see cref="OnChange"/> otherwise.
    /// </summary>
    public void SetContextRailCollapsed(bool collapsed)
    {
        if (_isContextRailCollapsed == collapsed)
        {
            return;
        }

        _isContextRailCollapsed = collapsed;
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the Jarvis AI chat panel open state.
    /// No-op when the value is unchanged; fires <see cref="OnChange"/> otherwise.
    /// </summary>
    public void SetJarvisOpen(bool open)
    {
        if (_isJarvisOpen == open)
        {
            return;
        }

        _isJarvisOpen = open;
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the RTL text-direction flag.
    /// Called by <c>MainLayout</c> once on startup after reading
    /// <c>document.documentElement.dir</c> via JS interop.
    /// No-op when the value is unchanged; fires <see cref="OnChange"/> otherwise.
    /// </summary>
    public void SetEnableRtl(bool enable)
    {
        if (_enableRtl == enable)
        {
            return;
        }

        _enableRtl = enable;
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the viewport-derived layout mode.
    /// Called by the JS resize observer bridge in <c>MainLayout</c>.
    /// No-op when the mode is unchanged; fires <see cref="OnChange"/> otherwise.
    /// </summary>
    public void SetLayoutMode(WorkspaceLayoutMode mode)
    {
        if (_layoutMode == mode)
        {
            return;
        }

        _layoutMode = mode;
        NotifyStateChanged();
    }

    // ── Notification ──────────────────────────────────────────────────────────

    private void NotifyStateChanged() => OnChange?.Invoke();

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears all <see cref="OnChange"/> subscribers to prevent memory leaks
    /// when <c>MainLayout</c> is torn down (Blazor Server circuit close, or
    /// hot-reload in development).
    /// </summary>
    public void Dispose()
    {
        // Nulling the event multicast delegate releases all subscriber
        // references in one assignment — safe because WASM is single-threaded.
        OnChange = null;
    }
}