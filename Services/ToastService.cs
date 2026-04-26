namespace WileyCoWeb.Services;

/// <summary>Severity level for a centralized toast notification.</summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Immutable descriptor for a single notification raised through <see cref="ToastService"/>.
/// </summary>
/// <param name="Title">Short heading shown in bold at the top of the toast.</param>
/// <param name="Content">Descriptive message body shown below the title.</param>
/// <param name="Level">Visual severity that controls the toast accent color.</param>
/// <param name="TimeoutMs">
/// Milliseconds before auto-dismiss.  Zero means the toast persists until the
/// user explicitly closes it with the close button.
/// </param>
public sealed record ToastRequest(
    string Title,
    string Content,
    ToastLevel Level = ToastLevel.Info,
    int TimeoutMs = 4000);

/// <summary>
/// Singleton notification bus.  Any injectable component or service calls the
/// Show* helpers to raise a notification; the shell-level <c>ToastHost</c>
/// component subscribes to <see cref="OnShow"/> and renders the toast via a
/// single, globally-scoped <c>SfToast</c> instance.
/// </summary>
/// <remarks>
/// Registered as a singleton in Blazor WASM (<see cref="ClientStartup"/>),
/// so the same instance is shared across all components in the application
/// lifetime.  No locking is required because Blazor WASM runs on a single
/// thread.
/// </remarks>
public sealed class ToastService
{
    /// <summary>
    /// Raised when any caller wants to display a notification.
    /// The shell-level <c>ToastHost</c> is the sole subscriber; it dispatches
    /// the actual <c>SfToast.ShowAsync</c> call on the Blazor rendering thread.
    /// </summary>
    public event Action<ToastRequest>? OnShow;

    // ── Severity-specific convenience methods ─────────────────────────────

    /// <summary>
    /// Shows a success toast (green accent, 4 s auto-dismiss by default).
    /// Use for completed operations: imports, saves, refreshes.
    /// </summary>
    public void ShowSuccess(string title, string content, int timeoutMs = 4000)
        => Raise(title, content, ToastLevel.Success, timeoutMs);

    /// <summary>
    /// Shows an error toast (red accent, 6 s auto-dismiss by default) so the
    /// user has time to read the failure message before it disappears.
    /// </summary>
    public void ShowError(string title, string content, int timeoutMs = 6000)
        => Raise(title, content, ToastLevel.Error, timeoutMs);

    /// <summary>
    /// Shows a warning toast (amber accent, 5 s auto-dismiss by default).
    /// Use for non-fatal advisory messages: duplicates blocked, missing fields.
    /// </summary>
    public void ShowWarning(string title, string content, int timeoutMs = 5000)
        => Raise(title, content, ToastLevel.Warning, timeoutMs);

    /// <summary>
    /// Shows an informational toast (blue accent, 4 s auto-dismiss by default).
    /// Use for neutral status updates and general feedback.
    /// </summary>
    public void ShowInfo(string title, string content, int timeoutMs = 4000)
        => Raise(title, content, ToastLevel.Info, timeoutMs);

    // ── Internal ──────────────────────────────────────────────────────────

    private void Raise(string title, string content, ToastLevel level, int timeoutMs)
        => OnShow?.Invoke(new ToastRequest(title, content, level, timeoutMs));
}
