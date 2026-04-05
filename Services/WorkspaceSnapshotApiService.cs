using System.Net.Http.Json;
using System.Text.Json;

namespace WileyCoWeb.Services;

public sealed class WorkspaceSnapshotApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly HttpClient httpClient;

    public WorkspaceSnapshotApiService(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<WorkspaceSnapshotSaveResponse> SaveRateSnapshotAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var content = JsonContent.Create(snapshot, options: JsonOptions);
        var response = await httpClient.PostAsync("api/workspace/snapshot", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Saving the workspace snapshot failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var savedSnapshot = await response.Content.ReadFromJsonAsync<WorkspaceSnapshotSaveResponse>(JsonOptions, cancellationToken);
        return savedSnapshot ?? throw new InvalidOperationException("The workspace snapshot save response was empty.");
    }
}

public sealed record WorkspaceSnapshotSaveResponse(long SnapshotId, string SnapshotName, string SavedAtUtc);