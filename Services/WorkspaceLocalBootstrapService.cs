using System.Net.Http.Json;
using WileyCoWeb.Contracts;

namespace WileyCoWeb.Services;

public sealed class WorkspaceLocalBootstrapService(HttpClient httpClient)
{
    private const string LocalBootstrapPath = "data/workspace-bootstrap.json";

    public Task<WorkspaceBootstrapData?> LoadAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<WorkspaceBootstrapData>(LocalBootstrapPath, cancellationToken);
}