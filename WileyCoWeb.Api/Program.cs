using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Net.Http.Json;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models.Amplify;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyCoWeb.Api;

public partial class Program
{
    private const string ScenarioRecordPrefix = "RecordType:Scenario";
    private const string RateSnapshotRecordPrefix = "RecordType:RateSnapshot";

    protected Program()
    {
    }

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("OpenWorkspaceClient", policy =>
            {
                policy.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin();
            });
        });

        builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new AppDbContextFactory(builder.Configuration));
        builder.Services.AddSingleton<WorkspaceSnapshotComposer>();
        builder.Services.AddSingleton<WorkspaceSnapshotExportArchiveService>();
        builder.Services.AddSingleton<QuickBooksImportService>();
        builder.Services.AddSingleton<QuickBooksImportAssistantService>();
        builder.Services.AddSingleton<WorkspaceAiAssistantService>();
        builder.Services.AddSingleton<UserContext>();
        builder.Services.AddSingleton<IUserContext>(sp => sp.GetRequiredService<UserContext>());
        builder.Services.AddSingleton<IConversationRepository, EfConversationRepository>();

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var userContext = context.RequestServices.GetRequiredService<UserContext>();
            PopulateUserContext(context, userContext);

            try
            {
                await next().ConfigureAwait(false);
            }
            finally
            {
                userContext.SetCurrentUser(null, null, null);
            }
        });

        app.UseCors("OpenWorkspaceClient");
        MapWorkspaceSnapshotEndpoints(app);

        await app.RunAsync();
    }

    private static void MapWorkspaceSnapshotEndpoints(WebApplication app)
    {
        MapWorkspaceSnapshotGetEndpoint(app);
        MapWorkspaceSnapshotPostEndpoint(app);
        MapWorkspaceBaselinePutEndpoint(app);
        MapWorkspaceScenarioListEndpoint(app);
        MapWorkspaceScenarioGetEndpoint(app);
        MapWorkspaceScenarioPostEndpoint(app);
        MapWorkspaceSnapshotExportsPostEndpoint(app);
        MapWorkspaceSnapshotExportsGetEndpoint(app);
        MapWorkspaceExportDownloadEndpoint(app);
        MapWorkspaceAiChatEndpoint(app);
        MapQuickBooksImportEndpoints(app);
    }

    private static void MapWorkspaceAiChatEndpoint(WebApplication app)
    {
        app.MapPost("/api/ai/chat", MapWorkspaceAiChatMessageEndpoint);
    }

    private static void MapQuickBooksImportEndpoints(WebApplication app)
    {
        app.MapPost("/api/imports/quickbooks/preview", MapQuickBooksPreviewEndpoint);
        app.MapPost("/api/imports/quickbooks/commit", MapQuickBooksCommitEndpoint);
        app.MapPost("/api/imports/quickbooks/assistant", MapQuickBooksAssistantEndpoint);
    }

    private static async Task<IResult> MapQuickBooksPreviewEndpoint(HttpRequest request, QuickBooksImportService importService, CancellationToken cancellationToken)
    {
        var importRequest = await ReadQuickBooksImportRequestAsync(request, cancellationToken);
        if (importRequest is null)
        {
            return Results.BadRequest("A QuickBooks export file, enterprise, and fiscal year are required.");
        }

        var preview = await importService.PreviewAsync(importRequest.FileBytes, importRequest.FileName, importRequest.SelectedEnterprise, importRequest.SelectedFiscalYear, cancellationToken);
        return Results.Ok(preview);
    }

    private static async Task<IResult> MapQuickBooksCommitEndpoint(HttpRequest request, QuickBooksImportService importService, CancellationToken cancellationToken)
    {
        var importRequest = await ReadQuickBooksImportRequestAsync(request, cancellationToken);
        if (importRequest is null)
        {
            return Results.BadRequest("A QuickBooks export file, enterprise, and fiscal year are required.");
        }

        var commitResult = await importService.CommitAsync(importRequest.FileBytes, importRequest.FileName, importRequest.SelectedEnterprise, importRequest.SelectedFiscalYear, cancellationToken);
        return commitResult.IsDuplicate
            ? Results.Conflict(commitResult)
            : Results.Ok(commitResult);
    }

    private static async Task<IResult> MapQuickBooksAssistantEndpoint(HttpRequest request, QuickBooksImportAssistantService assistantService, CancellationToken cancellationToken)
    {
        var guidanceRequest = await request.ReadFromJsonAsync<QuickBooksImportGuidanceRequest>(cancellationToken: cancellationToken);
        if (guidanceRequest is null || string.IsNullOrWhiteSpace(guidanceRequest.Question))
        {
            return Results.BadRequest("A question is required for QuickBooks import assistance.");
        }

        var guidance = await assistantService.AskAsync(guidanceRequest, cancellationToken);
        return Results.Ok(guidance);
    }

    private static async Task<IResult> MapWorkspaceAiChatMessageEndpoint(HttpRequest request, WorkspaceAiAssistantService assistantService, CancellationToken cancellationToken)
    {
        var chatRequest = await request.ReadFromJsonAsync<WorkspaceChatRequest>(cancellationToken: cancellationToken);
        if (chatRequest is null || string.IsNullOrWhiteSpace(chatRequest.Question))
        {
            return Results.BadRequest("A question is required for workspace chat.");
        }

        var chatResponse = await assistantService.AskAsync(chatRequest, cancellationToken);
        return Results.Ok(chatResponse);
    }

    private static void PopulateUserContext(HttpContext context, UserContext userContext)
    {
        var principal = context.User;

        var userId = ResolveClaim(principal, "sub")
            ?? ResolveClaim(principal, ClaimTypes.NameIdentifier)
            ?? context.Request.Headers["X-Wiley-User-Id"].FirstOrDefault()
            ?? "anonymous";

        var displayName = ResolveClaim(principal, "name")
            ?? ResolveClaim(principal, "preferred_username")
            ?? ResolveClaim(principal, ClaimTypes.Name)
            ?? ResolveClaim(principal, ClaimTypes.Email)?.Split('@', 2)[0]
            ?? context.Request.Headers["X-Wiley-User-Name"].FirstOrDefault()
            ?? "Guest";

        var email = ResolveClaim(principal, ClaimTypes.Email)
            ?? ResolveClaim(principal, "email")
            ?? context.Request.Headers["X-Wiley-User-Email"].FirstOrDefault();

        userContext.SetCurrentUser(userId, displayName, email);
    }

    private static string? ResolveClaim(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;

    private static async Task<QuickBooksImportRequest?> ReadQuickBooksImportRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file == null)
        {
            return null;
        }

        var selectedEnterprise = form["selectedEnterprise"].ToString();
        if (string.IsNullOrWhiteSpace(selectedEnterprise))
        {
            return null;
        }

        if (!int.TryParse(form["selectedFiscalYear"].ToString(), out var selectedFiscalYear) || selectedFiscalYear <= 0)
        {
            return null;
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        return new QuickBooksImportRequest(
            memoryStream.ToArray(),
            file.FileName,
            selectedEnterprise,
            selectedFiscalYear);
    }

    private sealed record QuickBooksImportRequest(byte[] FileBytes, string FileName, string SelectedEnterprise, int SelectedFiscalYear);
    private static void MapWorkspaceSnapshotGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/snapshot", async (
            string? enterprise,
            int? fiscalYear,
            WorkspaceSnapshotComposer composer,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await composer.BuildAsync(enterprise, fiscalYear, cancellationToken);
            return Results.Ok(snapshot);
        });
    }

    private static void MapWorkspaceSnapshotPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/snapshot", async (
            WorkspaceBootstrapData request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.SelectedEnterprise))
            {
                return Results.BadRequest("An enterprise name is required to save a snapshot.");
            }

            if (request.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("A valid fiscal year is required to save a snapshot.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var savedAt = DateTimeOffset.UtcNow;
            var snapshot = new BudgetSnapshot
            {
                SnapshotName = $"{request.SelectedEnterprise} FY{request.SelectedFiscalYear} rate snapshot",
                SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
                CreatedAt = savedAt,
                Notes = $"{RateSnapshotRecordPrefix}; Enterprise: {request.SelectedEnterprise}; FY: {request.SelectedFiscalYear}; Current rate: {request.CurrentRate:0.##}; Total costs: {request.TotalCosts:0.##}; Projected volume: {request.ProjectedVolume:0.##}",
                Payload = JsonSerializer.Serialize(request)
            };

            context.BudgetSnapshots.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/workspace/snapshot/{snapshot.Id}", new WorkspaceSnapshotSaveResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                snapshot.CreatedAt.ToString("O")));
        });
    }

    private static void MapWorkspaceBaselinePutEndpoint(WebApplication app)
    {
        app.MapPut("/api/workspace/baseline", async (
            WorkspaceBaselineUpdateRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            WorkspaceSnapshotComposer composer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.SelectedEnterprise))
            {
                return Results.BadRequest("A workspace enterprise is required.");
            }

            if (request.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("A valid fiscal year is required.");
            }

            if (request.ProjectedVolume <= 0)
            {
                return Results.BadRequest("Projected volume must be greater than zero.");
            }

            if (request.CurrentRate < 0 || request.TotalCosts < 0)
            {
                return Results.BadRequest("Workspace baseline values cannot be negative.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var enterprise = await context.Enterprises
                .FirstOrDefaultAsync(item => !item.IsDeleted && item.Name == request.SelectedEnterprise, cancellationToken);

            if (enterprise == null)
            {
                return Results.NotFound($"Enterprise '{request.SelectedEnterprise}' was not found.");
            }

            enterprise.CurrentRate = decimal.Round(request.CurrentRate, 2, MidpointRounding.AwayFromZero);
            enterprise.MonthlyExpenses = decimal.Round(request.TotalCosts, 2, MidpointRounding.AwayFromZero);
            enterprise.CitizenCount = Math.Max(1, decimal.ToInt32(decimal.Round(request.ProjectedVolume, 0, MidpointRounding.AwayFromZero)));
            enterprise.LastModified = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            var snapshot = await composer.BuildAsync(request.SelectedEnterprise, request.SelectedFiscalYear, cancellationToken);
            var savedAtUtc = DateTime.UtcNow.ToString("O");
            var response = new WorkspaceBaselineUpdateResponse(
                snapshot.SelectedEnterprise,
                snapshot.SelectedFiscalYear,
                savedAtUtc,
                $"Saved baseline values for {snapshot.SelectedEnterprise} FY {snapshot.SelectedFiscalYear}.",
                ToBootstrapData(snapshot));

            return Results.Ok(response);
        });
    }

    private static void MapWorkspaceScenarioListEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios", async (
            string? enterprise,
            int? fiscalYear,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshots = await context.BudgetSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.Notes != null && snapshot.Notes.Contains(ScenarioRecordPrefix))
                .OrderByDescending(snapshot => snapshot.CreatedAt)
                .ToListAsync(cancellationToken);

            var scenarios = new List<WorkspaceScenarioSummaryResponse>();
            foreach (var snapshot in snapshots)
            {
                var payload = TryDeserializeBootstrap(snapshot.Payload);
                if (payload == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(enterprise) &&
                    !string.Equals(payload.SelectedEnterprise, enterprise, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fiscalYear is > 0 && payload.SelectedFiscalYear != fiscalYear.Value)
                {
                    continue;
                }

                scenarios.Add(BuildScenarioSummary(snapshot, payload));
            }

            return Results.Ok(new WorkspaceScenarioCollectionResponse(scenarios));
        });
    }

    private static void MapWorkspaceScenarioGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/scenarios/{snapshotId:long}", async (
            long snapshotId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null || snapshot.Notes?.Contains(ScenarioRecordPrefix, StringComparison.Ordinal) != true)
            {
                return Results.NotFound();
            }

            var payload = TryDeserializeBootstrap(snapshot.Payload);
            return payload == null ? Results.BadRequest("The selected scenario does not contain a valid workspace payload.") : Results.Ok(payload);
        });
    }

    private static void MapWorkspaceScenarioPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/scenarios", async (
            WorkspaceScenarioSaveRequest request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            if (request.Snapshot == null)
            {
                return Results.BadRequest("A workspace snapshot is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ScenarioName))
            {
                return Results.BadRequest("A scenario name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Snapshot.SelectedEnterprise) || request.Snapshot.SelectedFiscalYear <= 0)
            {
                return Results.BadRequest("Scenario persistence requires a valid enterprise and fiscal year.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var savedAt = DateTimeOffset.UtcNow;
            var normalizedScenarioName = request.ScenarioName.Trim();
            var snapshot = new BudgetSnapshot
            {
                SnapshotName = $"{request.Snapshot.SelectedEnterprise} FY{request.Snapshot.SelectedFiscalYear} scenario {normalizedScenarioName}",
                SnapshotDate = DateOnly.FromDateTime(savedAt.UtcDateTime),
                CreatedAt = savedAt,
                Notes = BuildScenarioNotes(request, normalizedScenarioName),
                Payload = JsonSerializer.Serialize(request.Snapshot with { ActiveScenarioName = normalizedScenarioName, LastUpdatedUtc = savedAt.ToString("O") })
            };

            context.BudgetSnapshots.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);

            var payload = TryDeserializeBootstrap(snapshot.Payload) ?? request.Snapshot;
            return Results.Created($"/api/workspace/scenarios/{snapshot.Id}", BuildScenarioSummary(snapshot, payload, request.Description));
        });
    }

    private static void MapWorkspaceSnapshotExportsPostEndpoint(WebApplication app)
    {
        app.MapPost("/api/workspace/snapshot/{snapshotId:long}/exports", async (
            long snapshotId,
            WorkspaceSnapshotArtifactRequest? request,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .Include(item => item.ExportArtifacts)
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(snapshot.Payload))
            {
                return Results.BadRequest("The selected snapshot does not contain a payload that can be used to generate exports.");
            }

            var documents = WorkspaceSnapshotExportArchiveService.CreateDocuments(snapshot.Payload, request?.DocumentKinds);
            var normalizedKinds = documents.Select(document => document.DocumentKind).ToHashSet(StringComparer.Ordinal);

            if (request?.ReplaceExisting == true)
            {
                var existingArtifacts = snapshot.ExportArtifacts
                    .Where(artifact => normalizedKinds.Contains(artifact.DocumentKind))
                    .ToList();

                if (existingArtifacts.Count > 0)
                {
                    context.BudgetSnapshotArtifacts.RemoveRange(existingArtifacts);
                }
            }

            var createdAt = DateTimeOffset.UtcNow;
            var artifacts = documents.Select(document => new BudgetSnapshotArtifact
            {
                BudgetSnapshotId = snapshot.Id,
                DocumentKind = document.DocumentKind,
                FileName = document.FileName,
                ContentType = document.ContentType,
                SizeBytes = document.Content.LongLength,
                CreatedAt = createdAt,
                Payload = document.Content
            }).ToList();

            context.BudgetSnapshotArtifacts.AddRange(artifacts);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new WorkspaceSnapshotArtifactBatchResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                artifacts.Select(BuildArtifactSummary).ToList()));
        });
    }

    private static void MapWorkspaceSnapshotExportsGetEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/snapshot/{snapshotId:long}/exports", async (
            long snapshotId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var snapshot = await context.BudgetSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);

            if (snapshot == null)
            {
                return Results.NotFound();
            }

            var artifacts = await context.BudgetSnapshotArtifacts
                .AsNoTracking()
                .Where(item => item.BudgetSnapshotId == snapshotId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            return Results.Ok(new WorkspaceSnapshotArtifactBatchResponse(
                snapshot.Id,
                snapshot.SnapshotName,
                artifacts.Select(BuildArtifactSummary).ToList()));
        });
    }

    private static void MapWorkspaceExportDownloadEndpoint(WebApplication app)
    {
        app.MapGet("/api/workspace/exports/{artifactId:long}", async (
            long artifactId,
            IDbContextFactory<AppDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var artifact = await context.BudgetSnapshotArtifacts
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == artifactId, cancellationToken);

            if (artifact == null)
            {
                return Results.NotFound();
            }

            return Results.File(artifact.Payload, artifact.ContentType, artifact.FileName);
        });
    }

    internal static WorkspaceSnapshotArtifactSummary BuildArtifactSummary(BudgetSnapshotArtifact artifact)
    {
        return new WorkspaceSnapshotArtifactSummary(
            artifact.Id,
            artifact.BudgetSnapshotId,
            artifact.DocumentKind,
            artifact.FileName,
            artifact.ContentType,
            artifact.SizeBytes,
            artifact.CreatedAt.ToString("O"),
            $"/api/workspace/exports/{artifact.Id}");
    }

    private static WorkspaceBootstrapData? TryDeserializeBootstrap(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkspaceBootstrapData>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildScenarioNotes(WorkspaceScenarioSaveRequest request, string normalizedScenarioName)
    {
        return $"{ScenarioRecordPrefix}; Enterprise: {request.Snapshot.SelectedEnterprise}; FY: {request.Snapshot.SelectedFiscalYear}; Scenario: {normalizedScenarioName}; Description: {request.Description}";
    }

    private static WorkspaceScenarioSummaryResponse BuildScenarioSummary(BudgetSnapshot snapshot, WorkspaceBootstrapData payload, string? descriptionOverride = null)
    {
        var scenarioName = string.IsNullOrWhiteSpace(payload.ActiveScenarioName) ? snapshot.SnapshotName : payload.ActiveScenarioName;
        var description = descriptionOverride;
        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(snapshot.Notes))
        {
            description = ExtractDescription(snapshot.Notes);
        }

        return new WorkspaceScenarioSummaryResponse(
            snapshot.Id,
            scenarioName,
            payload.SelectedEnterprise,
            payload.SelectedFiscalYear,
            snapshot.CreatedAt.ToString("O"),
            payload.CurrentRate,
            payload.TotalCosts,
            payload.ProjectedVolume,
            payload.ScenarioItems?.Sum(item => item.Cost) ?? 0m,
            payload.ScenarioItems?.Count ?? 0,
            description);
    }

    private static WorkspaceBootstrapData ToBootstrapData(WorkspaceSnapshotResponse snapshot)
    {
        return new WorkspaceBootstrapData(
            snapshot.SelectedEnterprise,
            snapshot.SelectedFiscalYear,
            snapshot.ActiveScenarioName,
            snapshot.CurrentRate,
            snapshot.TotalCosts,
            snapshot.ProjectedVolume,
            snapshot.LastUpdatedUtc)
        {
            EnterpriseOptions = snapshot.EnterpriseOptions,
            FiscalYearOptions = snapshot.FiscalYearOptions,
            CustomerServiceOptions = snapshot.CustomerServiceOptions,
            CustomerCityLimitOptions = snapshot.CustomerCityLimitOptions,
            ScenarioItems = snapshot.ScenarioItems,
            CustomerRows = snapshot.CustomerRows,
            ProjectionRows = snapshot.ProjectionRows
        };
    }

    private static string? ExtractDescription(string notes)
    {
        const string marker = "Description: ";
        var markerIndex = notes.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var value = notes[(markerIndex + marker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
