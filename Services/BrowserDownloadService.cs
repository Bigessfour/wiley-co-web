using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace WileyCoWeb.Services;

public sealed class BrowserDownloadService
{
    private readonly IJSRuntime jsRuntime;
    private readonly ILogger<BrowserDownloadService>? logger;

    public BrowserDownloadService(IJSRuntime jsRuntime, ILogger<BrowserDownloadService>? logger = null)
    {
        this.jsRuntime = jsRuntime;
        this.logger = logger;
    }

    public ValueTask DownloadAsync(WorkspaceExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        logger?.LogInformation("Starting browser download for {FileName} ({ContentType}, {ByteCount} bytes)", document.FileName, document.ContentType, document.Content.LongLength);

        return jsRuntime.InvokeVoidAsync(
            "wileyDownloads.saveFileFromBase64",
            document.FileName,
            document.ContentType,
            Convert.ToBase64String(document.Content));
    }
}