using Microsoft.JSInterop;

namespace WileyCoWeb.Services;

public sealed class BrowserDownloadService
{
    private readonly IJSRuntime jsRuntime;

    public BrowserDownloadService(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public ValueTask DownloadAsync(WorkspaceExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return jsRuntime.InvokeVoidAsync(
            "wileyDownloads.saveFileFromBase64",
            document.FileName,
            document.ContentType,
            Convert.ToBase64String(document.Content));
    }
}