using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Amplify;

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

var app = builder.Build();

app.UseCors("OpenWorkspaceClient");

app.MapGet("/api/workspace/snapshot", async (
    string? enterprise,
    int? fiscalYear,
    WorkspaceSnapshotComposer composer,
    CancellationToken cancellationToken) =>
{
    var snapshot = await composer.BuildAsync(enterprise, fiscalYear, cancellationToken);
    return Results.Ok(snapshot);
});

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
        Notes = $"Enterprise: {request.SelectedEnterprise}; FY: {request.SelectedFiscalYear}; Current rate: {request.CurrentRate:0.##}; Total costs: {request.TotalCosts:0.##}; Projected volume: {request.ProjectedVolume:0.##}",
        Payload = JsonSerializer.Serialize(request)
    };

    context.BudgetSnapshots.Add(snapshot);
    await context.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/workspace/snapshot/{snapshot.Id}", new WorkspaceSnapshotSaveResponse(
        snapshot.Id,
        snapshot.SnapshotName,
        snapshot.CreatedAt.ToString("O")));
});

app.Run();

internal sealed class WorkspaceSnapshotComposer
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public WorkspaceSnapshotComposer(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<WorkspaceSnapshotResponse> BuildAsync(string? enterpriseName, int? fiscalYear, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var enterprises = await context.Enterprises
            .AsNoTracking()
            .Where(enterprise => !enterprise.IsDeleted)
            .OrderBy(enterprise => enterprise.Name)
            .ToListAsync(cancellationToken);

        var selectedEnterprise = enterprises.FirstOrDefault(enterprise =>
            string.Equals(enterprise.Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
            ?? enterprises.FirstOrDefault()
            ?? new Enterprise();

        var budgetYears = await context.BudgetEntries
            .AsNoTracking()
            .Select(entry => entry.FiscalYear)
            .Distinct()
            .OrderBy(year => year)
            .ToListAsync(cancellationToken);

        var selectedFiscalYear = fiscalYear is > 0
            ? fiscalYear.Value
            : budgetYears.Count > 0
                ? budgetYears[^1]
                : DateTime.UtcNow.Year;

        var customers = await context.UtilityCustomers
            .AsNoTracking()
            .OrderBy(customer => customer.AccountNumber)
            .ToListAsync(cancellationToken);

        var customerRows = customers
            .Select(customer => new CustomerRow(
                string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.AccountNumber : customer.DisplayName,
                customer.CustomerTypeDescription,
                customer.ServiceLocation == global::WileyWidget.Models.ServiceLocation.InsideCityLimits ? "Yes" : "No"))
            .ToList();

        var serviceOptions = customerRows
            .Select(customer => customer.Service)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(service => service)
            .ToList();
        serviceOptions.Insert(0, "All Services");

        var currentRate = selectedEnterprise.CurrentRate > 0 ? selectedEnterprise.CurrentRate : 0m;
        var totalCosts = selectedEnterprise.MonthlyExpenses > 0 ? selectedEnterprise.MonthlyExpenses : 0m;
        var projectedVolume = selectedEnterprise.CitizenCount > 0 ? selectedEnterprise.CitizenCount : 0m;
        var recommendedRate = projectedVolume == 0 ? 0m : Math.Round(totalCosts / projectedVolume, 2, MidpointRounding.AwayFromZero);
        var adjustedRecommendedRate = projectedVolume == 0 ? 0m : Math.Round((totalCosts + Math.Max(0m, totalCosts * 0.08m)) / projectedVolume, 2, MidpointRounding.AwayFromZero);

        var projectionRows = BuildProjectionRows(selectedFiscalYear, currentRate, recommendedRate, adjustedRecommendedRate);
        var scenarioItems = await BuildScenarioItemsAsync(context, selectedFiscalYear, cancellationToken);

        return new WorkspaceSnapshotResponse
        {
            SelectedEnterprise = selectedEnterprise.Name,
            SelectedFiscalYear = selectedFiscalYear,
            ActiveScenarioName = $"{selectedEnterprise.Name} planning snapshot",
            CurrentRate = currentRate,
            TotalCosts = totalCosts,
            ProjectedVolume = projectedVolume,
            LastUpdatedUtc = DateTime.UtcNow.ToString("O"),
            EnterpriseOptions = enterprises.Select(enterprise => enterprise.Name).ToList(),
            FiscalYearOptions = budgetYears.Count > 0 ? budgetYears : [selectedFiscalYear],
            CustomerServiceOptions = serviceOptions,
            CustomerCityLimitOptions = ["All", "Yes", "No"],
            ScenarioItems = scenarioItems,
            CustomerRows = customerRows,
            ProjectionRows = projectionRows
        };
    }

    private static List<ProjectionRow> BuildProjectionRows(int fiscalYear, decimal currentRate, decimal recommendedRate, decimal adjustedRecommendedRate)
    {
        var previousYear = Math.Max(1, fiscalYear - 1);
        var nextYear = fiscalYear + 1;
        var followingYear = fiscalYear + 2;

        return
        [
            new ProjectionRow($"FY{previousYear % 100:00}", Math.Round(currentRate * 0.94m, 2, MidpointRounding.AwayFromZero)),
            new ProjectionRow($"FY{fiscalYear % 100:00}", currentRate),
            new ProjectionRow($"FY{nextYear % 100:00}", recommendedRate),
            new ProjectionRow($"FY{followingYear % 100:00}", adjustedRecommendedRate)
        ];
    }

    private static async Task<List<WorkspaceScenarioItemData>> BuildScenarioItemsAsync(global::WileyWidget.Data.AppDbContext context, int fiscalYear, CancellationToken cancellationToken)
    {
        var topDepartments = await context.BudgetEntries
            .AsNoTracking()
            .Where(entry => entry.FiscalYear == fiscalYear)
            .GroupBy(entry => entry.Department.Name)
            .Select(group => new
            {
                Name = group.Key,
                Amount = group.Sum(entry => entry.BudgetedAmount)
            })
            .OrderByDescending(group => group.Amount)
            .Take(3)
            .ToListAsync(cancellationToken);

        return topDepartments
            .Select((department, index) => new WorkspaceScenarioItemData(
                Guid.NewGuid(),
                string.IsNullOrWhiteSpace(department.Name) ? $"Priority item {index + 1}" : department.Name,
                Math.Round(Math.Max(0m, department.Amount * 0.05m), 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

}

public sealed record WorkspaceSnapshotResponse
{
    public string SelectedEnterprise { get; init; } = string.Empty;
    public int SelectedFiscalYear { get; init; }
    public string ActiveScenarioName { get; init; } = string.Empty;
    public decimal? CurrentRate { get; init; }
    public decimal? TotalCosts { get; init; }
    public decimal? ProjectedVolume { get; init; }
    public string? LastUpdatedUtc { get; init; }
    public List<string>? EnterpriseOptions { get; init; }
    public List<int>? FiscalYearOptions { get; init; }
    public List<string>? CustomerServiceOptions { get; init; }
    public List<string>? CustomerCityLimitOptions { get; init; }
    public List<WorkspaceScenarioItemData>? ScenarioItems { get; init; }
    public List<CustomerRow>? CustomerRows { get; init; }
    public List<ProjectionRow>? ProjectionRows { get; init; }
}

public sealed record WorkspaceScenarioItemData(Guid Id, string Name, decimal Cost);

public sealed record CustomerRow(string Name, string Service, string CityLimits);

public sealed record ProjectionRow(string Year, decimal Rate);

public sealed record WorkspaceSnapshotSaveResponse(long SnapshotId, string SnapshotName, string SavedAtUtc);

public sealed record WorkspaceBootstrapData(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string ActiveScenarioName,
    decimal? CurrentRate,
    decimal? TotalCosts,
    decimal? ProjectedVolume,
    string? LastUpdatedUtc)
{
    public List<CustomerRow>? CustomerRows { get; init; }

    public List<ProjectionRow>? ProjectionRows { get; init; }

    public List<WorkspaceScenarioItemData>? ScenarioItems { get; init; }
}

public partial class Program
{
}
