using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyCoWeb.Contracts;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{
    public async Task<WorkspaceReferenceDataImportResponse> ImportAsync(
        WorkspaceReferenceDataImportRequest? request,
        string contentRootPath,
        CancellationToken cancellationToken)
        => await ImportCoreAsync(request, contentRootPath, cancellationToken).ConfigureAwait(false);

    private async Task<WorkspaceReferenceDataImportResponse> ImportCoreAsync(WorkspaceReferenceDataImportRequest? request, string contentRootPath, CancellationToken cancellationToken) { var importContext = CreateImportRequestContext(request, contentRootPath); await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false); var enterpriseImportSummary = await ImportEnterpriseSourcesAsync(context, importContext.Sources, importContext.ResolvedImportPath, importContext.ImportedAtUtc, importContext.ApplyDefaultEnterpriseBaselines, cancellationToken).ConfigureAwait(false); var utilityCustomerImportSummary = await ImportUtilityCustomersAsync(context, importContext.ResolvedImportPath, importContext.ImportedAtUtc, cancellationToken).ConfigureAwait(false); var ledgerImportSummary = await ImportSampleLedgerDataIfRequestedAsync(importContext.ResolvedImportPath, request?.IncludeSampleLedgerData == true, cancellationToken).ConfigureAwait(false); return FinalizeImportAsync(importContext.ResolvedImportPath, importContext.ImportedAtUtc, importContext.Sources, enterpriseImportSummary, utilityCustomerImportSummary, ledgerImportSummary, cancellationToken); }

    private ImportRequestContext CreateImportRequestContext(WorkspaceReferenceDataImportRequest? request, string contentRootPath) { var resolvedImportPath = ResolveImportDataPath(request?.ImportDataPath, contentRootPath); EnsureImportDataPathExists(resolvedImportPath); return new ImportRequestContext(resolvedImportPath, GetEnterpriseSourcesOrThrow(resolvedImportPath), DateTime.UtcNow, request?.ApplyDefaultEnterpriseBaselines == true); }

    private static int GetDiscoveredCustomerRowCount(IReadOnlyList<EnterpriseReferenceSource> sources)
        => sources.Sum(item => item.DiscoveredCustomerReferenceRows);

    private async Task<LedgerImportSummary> ImportSampleLedgerDataIfRequestedAsync(string resolvedImportPath, bool includeSampleLedgerData, CancellationToken cancellationToken) => includeSampleLedgerData ? await ImportSampleLedgerDataAsync(resolvedImportPath, cancellationToken).ConfigureAwait(false) : LedgerImportSummary.NotRequested();

    private WorkspaceReferenceDataImportResponse FinalizeImportAsync(string resolvedImportPath, DateTime importedAtUtc, IReadOnlyList<EnterpriseReferenceSource> sources, EnterpriseImportSummary enterpriseImportSummary, UtilityCustomerImportSummary utilityCustomerImportSummary, LedgerImportSummary ledgerImportSummary, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); var discoveredCustomerRows = GetDiscoveredCustomerRowCount(sources); LogImportSummary(resolvedImportPath, enterpriseImportSummary, utilityCustomerImportSummary, ledgerImportSummary, discoveredCustomerRows); return BuildImportResponse(resolvedImportPath, importedAtUtc, sources, enterpriseImportSummary, utilityCustomerImportSummary, ledgerImportSummary, discoveredCustomerRows); }

    private static void EnsureImportDataPathExists(string resolvedImportPath) { if (!Directory.Exists(resolvedImportPath)) { throw new DirectoryNotFoundException($"Import data folder '{resolvedImportPath}' was not found."); } }

    private static List<EnterpriseReferenceSource> GetEnterpriseSourcesOrThrow(string resolvedImportPath) { var sources = DiscoverEnterpriseSources(resolvedImportPath); if (sources.Count == 0) { throw new InvalidOperationException($"No supported QuickBooks reference files were discovered in '{resolvedImportPath}'."); } return sources; }

    private async Task<EnterpriseImportSummary> ImportEnterpriseSourcesAsync(AppDbContext context, IReadOnlyList<EnterpriseReferenceSource> sources, string resolvedImportPath, DateTime importedAtUtc, bool applyDefaultEnterpriseBaselines, CancellationToken cancellationToken)
    {
        var stats = new EnterpriseImportStats();
        foreach (var source in sources.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            await UpsertEnterpriseAsync(context, source, resolvedImportPath, importedAtUtc, applyDefaultEnterpriseBaselines, stats, cancellationToken).ConfigureAwait(false);
        }

        EnsureCanonicalEnterprises(context, importedAtUtc, applyDefaultEnterpriseBaselines, stats);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        CollapseDuplicateCanonicalEnterprises(context, importedAtUtc);
        ArchiveNonCanonicalEnterprises(context, importedAtUtc);
        EnsureEnterpriseSupportEntities(context);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return stats.ToSummary();
    }

    private async Task UpsertEnterpriseAsync(AppDbContext context, EnterpriseReferenceSource source, string resolvedImportPath, DateTime importedAtUtc, bool applyDefaultEnterpriseBaselines, EnterpriseImportStats stats, CancellationToken cancellationToken)
    {
        var enterprise = await LoadOrCreateEnterpriseAsync(context, source, importedAtUtc, stats, cancellationToken).ConfigureAwait(false);
        ApplyImportedEnterpriseChanges(enterprise, source, resolvedImportPath, applyDefaultEnterpriseBaselines, stats);
    }

    private async Task<Enterprise> LoadOrCreateEnterpriseAsync(AppDbContext context, EnterpriseReferenceSource source, DateTime importedAtUtc, EnterpriseImportStats stats, CancellationToken cancellationToken) => LoadOrCreateEnterpriseCore(await FindEnterpriseAsync(context, source, cancellationToken).ConfigureAwait(false), context, source, importedAtUtc, stats);

    private async Task<Enterprise?> FindEnterpriseAsync(AppDbContext context, EnterpriseReferenceSource source, CancellationToken cancellationToken) => await context.Enterprises.FirstOrDefaultAsync(item => item.Name == source.Name, cancellationToken).ConfigureAwait(false);

    private static Enterprise LoadOrCreateEnterpriseCore(Enterprise? enterprise, AppDbContext context, EnterpriseReferenceSource source, DateTime importedAtUtc, EnterpriseImportStats stats) => enterprise is null ? RegisterImportedEnterprise(context, source, importedAtUtc, stats) : RegisterUpdatedEnterprise(enterprise, source, importedAtUtc, stats);

    private static Enterprise RegisterImportedEnterprise(AppDbContext context, EnterpriseReferenceSource source, DateTime importedAtUtc, EnterpriseImportStats stats)
    {
        var enterprise = CreateImportedEnterprise(source, importedAtUtc);
        context.Enterprises.Add(enterprise);
        stats.ImportedCount++;
        stats.TrackEnterpriseName(enterprise.Name);
        return enterprise;
    }

    private static Enterprise RegisterUpdatedEnterprise(Enterprise enterprise, EnterpriseReferenceSource source, DateTime importedAtUtc, EnterpriseImportStats stats)
    {
        UpdateImportedEnterprise(enterprise, source, importedAtUtc);
        stats.UpdatedCount++;
        stats.TrackEnterpriseName(enterprise.Name);
        return enterprise;
    }

    private void ApplyImportedEnterpriseChanges(Enterprise enterprise, EnterpriseReferenceSource source, string resolvedImportPath, bool applyDefaultEnterpriseBaselines, EnterpriseImportStats stats) { ApplyImportedEnterpriseMetadata(enterprise, source, resolvedImportPath); if (applyDefaultEnterpriseBaselines && ApplyDefaultBaseline(enterprise)) stats.SeededBaselineCount++; }

    private void EnsureCanonicalEnterprises(AppDbContext context, DateTime importedAtUtc, bool applyDefaultEnterpriseBaselines, EnterpriseImportStats stats)
    {
        var existingEnterprises = context.Enterprises.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var seed in WorkspaceEnterpriseSeedCatalog.All)
        {
            if (!existingEnterprises.TryGetValue(seed.Name, out var enterprise))
            {
                enterprise = new Enterprise
                {
                    Name = seed.Name,
                    Type = seed.Type,
                    CurrentRate = seed.CurrentRate,
                    MonthlyExpenses = seed.MonthlyExpenses,
                    CitizenCount = seed.CustomerCount,
                    CreatedDate = importedAtUtc,
                    ModifiedDate = importedAtUtc,
                    LastModified = importedAtUtc,
                    CreatedBy = nameof(WorkspaceReferenceDataImportService),
                    ModifiedBy = nameof(WorkspaceReferenceDataImportService),
                    Description = Truncate("Seeded to keep the canonical Wiley enterprise catalog intact.", 500),
                    Notes = Truncate($"Seeded as part of workspace reference-data import for {seed.Name}.", 500),
                    IsDeleted = false
                };

                context.Enterprises.Add(enterprise);
                existingEnterprises[seed.Name] = enterprise;
                stats.ImportedCount++;

                if (applyDefaultEnterpriseBaselines)
                {
                    stats.SeededBaselineCount++;
                }
            }
            else
            {
                enterprise.IsDeleted = false;
                enterprise.ModifiedDate = importedAtUtc;
                enterprise.ModifiedBy = nameof(WorkspaceReferenceDataImportService);
                enterprise.LastModified = importedAtUtc;
                enterprise.Type ??= seed.Type;
            }

            stats.TrackEnterpriseName(enterprise.Name);

            if (applyDefaultEnterpriseBaselines && ApplyDefaultBaseline(enterprise))
            {
                stats.SeededBaselineCount++;
            }
        }
    }

    private static void EnsureEnterpriseSupportEntities(AppDbContext context)
    {
        foreach (var seed in WorkspaceEnterpriseSeedCatalog.All)
        {
            var currentCharge = context.DepartmentCurrentCharges.FirstOrDefault(item => item.IsActive && item.Department == seed.DepartmentName);
            if (currentCharge is null)
            {
                context.DepartmentCurrentCharges.Add(new DepartmentCurrentCharge
                {
                    Department = seed.DepartmentName,
                    CurrentCharge = seed.CurrentRate,
                    CustomerCount = seed.CustomerCount,
                    UpdatedBy = nameof(WorkspaceReferenceDataImportService),
                    Notes = $"Seeded from {seed.Name} to keep enterprise rates isolated.",
                    IsActive = true
                });
            }

            var goal = context.DepartmentGoals.FirstOrDefault(item => item.IsActive && item.Department == seed.DepartmentName);
            if (goal is null)
            {
                context.DepartmentGoals.Add(new DepartmentGoal
                {
                    Department = seed.DepartmentName,
                    AdjustmentFactor = seed.GoalAdjustmentFactor,
                    TargetProfitMarginPercent = seed.TargetProfitMarginPercent,
                    RecommendationText = $"{seed.Name} should remain self-supporting without cross-enterprise subsidy.",
                    Source = nameof(WorkspaceReferenceDataImportService),
                    IsActive = true
                });
            }

            if (string.Equals(seed.Name, "Apartments", StringComparison.OrdinalIgnoreCase))
            {
                var apartmentEnterprise = context.Enterprises.FirstOrDefault(item => !item.IsDeleted && string.Equals(item.Name, seed.Name, StringComparison.OrdinalIgnoreCase));
                if (apartmentEnterprise is not null && !context.Set<ApartmentUnitType>().Any(item => item.EnterpriseId == apartmentEnterprise.Id && !item.IsDeleted))
                {
                    context.Set<ApartmentUnitType>().AddRange(
                        new ApartmentUnitType
                        {
                            EnterpriseId = apartmentEnterprise.Id,
                            Name = "2 Bedroom",
                            BedroomCount = 2,
                            UnitCount = 8,
                            MonthlyRent = 444.44m,
                            CreatedBy = nameof(WorkspaceReferenceDataImportService),
                            ModifiedBy = nameof(WorkspaceReferenceDataImportService),
                            IsDeleted = false
                        },
                        new ApartmentUnitType
                        {
                            EnterpriseId = apartmentEnterprise.Id,
                            Name = "3 Bedroom",
                            BedroomCount = 3,
                            UnitCount = 8,
                            MonthlyRent = 555.55m,
                            CreatedBy = nameof(WorkspaceReferenceDataImportService),
                            ModifiedBy = nameof(WorkspaceReferenceDataImportService),
                            IsDeleted = false
                        });
                }
            }
        }
    }

    private static void ArchiveNonCanonicalEnterprises(AppDbContext context, DateTime importedAtUtc)
    {
        var canonicalNames = WorkspaceEnterpriseSeedCatalog.All
            .Select(seed => seed.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var enterprise in context.Enterprises.Where(item => !item.IsDeleted && !canonicalNames.Contains(item.Name)))
        {
            enterprise.IsDeleted = true;
            enterprise.ModifiedDate = importedAtUtc;
            enterprise.ModifiedBy = nameof(WorkspaceReferenceDataImportService);
            enterprise.LastModified = importedAtUtc;
        }
    }

    private static void CollapseDuplicateCanonicalEnterprises(AppDbContext context, DateTime importedAtUtc)
    {
        foreach (var canonicalName in WorkspaceEnterpriseSeedCatalog.All.Select(seed => seed.Name))
        {
            var duplicates = context.Enterprises
                .Where(item => !item.IsDeleted && item.Name == canonicalName)
                .OrderBy(item => item.Id)
                .ToList();

            if (duplicates.Count <= 1)
            {
                continue;
            }

            foreach (var duplicate in duplicates.Skip(1))
            {
                duplicate.IsDeleted = true;
                duplicate.ModifiedDate = importedAtUtc;
                duplicate.ModifiedBy = nameof(WorkspaceReferenceDataImportService);
                duplicate.LastModified = importedAtUtc;
            }
        }
    }

    private static Enterprise CreateImportedEnterprise(EnterpriseReferenceSource source, DateTime importedAtUtc)
    {
        return new Enterprise { Name = source.Name, CurrentRate = 0m, MonthlyExpenses = 0m, CitizenCount = Math.Max(1, source.EstimatedCitizenCount), CreatedDate = importedAtUtc, ModifiedDate = importedAtUtc, LastModified = importedAtUtc, CreatedBy = nameof(WorkspaceReferenceDataImportService), ModifiedBy = nameof(WorkspaceReferenceDataImportService), IsDeleted = false };
    }

    private static void UpdateImportedEnterprise(Enterprise enterprise, EnterpriseReferenceSource source, DateTime importedAtUtc) { enterprise.ModifiedDate = importedAtUtc; enterprise.ModifiedBy = nameof(WorkspaceReferenceDataImportService); enterprise.LastModified = importedAtUtc; enterprise.IsDeleted = false; if (enterprise.CitizenCount <= 0) { enterprise.CitizenCount = Math.Max(1, source.EstimatedCitizenCount); } }

    private void ApplyImportedEnterpriseMetadata(Enterprise enterprise, EnterpriseReferenceSource source, string resolvedImportPath) { enterprise.Type = source.Type; enterprise.Description = Truncate($"Imported from QuickBooks reference data in {resolvedImportPath}.", 500); enterprise.Notes = Truncate(BuildEnterpriseNotes(source), 500); }

    private void LogImportSummary(string resolvedImportPath, EnterpriseImportSummary enterpriseImportSummary, UtilityCustomerImportSummary utilityCustomerImportSummary, LedgerImportSummary ledgerImportSummary, int discoveredCustomerRows) { logger.LogInformation("Imported workspace reference data from {ImportPath}: imported={Imported}, updated={Updated}, customerRows={CustomerRows}, importedCustomers={ImportedCustomers}, ledgerFiles={LedgerFiles}, ledgerRows={LedgerRows}, baselines={BaselineCount}", resolvedImportPath, enterpriseImportSummary.ImportedCount, enterpriseImportSummary.UpdatedCount, discoveredCustomerRows, utilityCustomerImportSummary.ImportedCount, ledgerImportSummary.ImportedFileCount, ledgerImportSummary.ImportedRowCount, enterpriseImportSummary.SeededBaselineCount); }

    private static WorkspaceReferenceDataImportResponse BuildImportResponse(string resolvedImportPath, DateTime importedAtUtc, IReadOnlyList<EnterpriseReferenceSource> sources, EnterpriseImportSummary enterpriseImportSummary, UtilityCustomerImportSummary utilityCustomerImportSummary, LedgerImportSummary ledgerImportSummary, int discoveredCustomerRows)
        => new WorkspaceReferenceDataImportResponse(resolvedImportPath, importedAtUtc.ToString("O", CultureInfo.InvariantCulture), enterpriseImportSummary.ImportedCount, enterpriseImportSummary.UpdatedCount, discoveredCustomerRows, utilityCustomerImportSummary.ImportedCount, utilityCustomerImportSummary.Status, WorkspaceEnterpriseSeedCatalog.All.Select(seed => seed.Name).ToArray(), ledgerImportSummary.ImportedFileCount, ledgerImportSummary.ImportedRowCount, ledgerImportSummary.Status, enterpriseImportSummary.SeededBaselineCount);

    private sealed class EnterpriseImportStats
    {
        private readonly HashSet<string> enterpriseNames = new(StringComparer.OrdinalIgnoreCase);

        public int ImportedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int SeededBaselineCount { get; set; }

        public void TrackEnterpriseName(string? enterpriseName)
        {
            if (!string.IsNullOrWhiteSpace(enterpriseName))
            {
                enterpriseNames.Add(enterpriseName.Trim());
            }
        }

        public EnterpriseImportSummary ToSummary()
            => new(
                ImportedCount,
                UpdatedCount,
                SeededBaselineCount,
                enterpriseNames
                    .OrderBy(WorkspaceEnterpriseSortOrder)
                    .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        private static int WorkspaceEnterpriseSortOrder(string enterpriseName)
        {
            for (var index = 0; index < WorkspaceEnterpriseSeedCatalog.All.Count; index++)
            {
                if (string.Equals(WorkspaceEnterpriseSeedCatalog.All[index].Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return int.MaxValue;
        }
    }

    private sealed record EnterpriseImportSummary(int ImportedCount, int UpdatedCount, int SeededBaselineCount, IReadOnlyList<string> EnterpriseNames);

    private sealed record ImportRequestContext(
        string ResolvedImportPath,
        IReadOnlyList<EnterpriseReferenceSource> Sources,
        DateTime ImportedAtUtc,
        bool ApplyDefaultEnterpriseBaselines);
}
