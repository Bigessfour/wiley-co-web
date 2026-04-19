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

    private async Task<EnterpriseImportSummary> ImportEnterpriseSourcesAsync(AppDbContext context, IReadOnlyList<EnterpriseReferenceSource> sources, string resolvedImportPath, DateTime importedAtUtc, bool applyDefaultEnterpriseBaselines, CancellationToken cancellationToken) { var stats = new EnterpriseImportStats(); foreach (var source in sources.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)) { await UpsertEnterpriseAsync(context, source, resolvedImportPath, importedAtUtc, applyDefaultEnterpriseBaselines, stats, cancellationToken).ConfigureAwait(false); } await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); return stats.ToSummary(); }

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
        return enterprise;
    }

    private static Enterprise RegisterUpdatedEnterprise(Enterprise enterprise, EnterpriseReferenceSource source, DateTime importedAtUtc, EnterpriseImportStats stats)
    {
        UpdateImportedEnterprise(enterprise, source, importedAtUtc);
        stats.UpdatedCount++;
        return enterprise;
    }

    private void ApplyImportedEnterpriseChanges(Enterprise enterprise, EnterpriseReferenceSource source, string resolvedImportPath, bool applyDefaultEnterpriseBaselines, EnterpriseImportStats stats) { ApplyImportedEnterpriseMetadata(enterprise, source, resolvedImportPath); if (applyDefaultEnterpriseBaselines && ApplyDefaultBaseline(enterprise)) stats.SeededBaselineCount++; }

    private static Enterprise CreateImportedEnterprise(EnterpriseReferenceSource source, DateTime importedAtUtc)
    {
        return new Enterprise { Name = source.Name, CurrentRate = 0m, MonthlyExpenses = 0m, CitizenCount = Math.Max(1, source.EstimatedCitizenCount), CreatedDate = importedAtUtc, ModifiedDate = importedAtUtc, LastModified = importedAtUtc, CreatedBy = nameof(WorkspaceReferenceDataImportService), ModifiedBy = nameof(WorkspaceReferenceDataImportService), IsDeleted = false };
    }

    private static void UpdateImportedEnterprise(Enterprise enterprise, EnterpriseReferenceSource source, DateTime importedAtUtc) { enterprise.ModifiedDate = importedAtUtc; enterprise.ModifiedBy = nameof(WorkspaceReferenceDataImportService); enterprise.LastModified = importedAtUtc; enterprise.IsDeleted = false; if (enterprise.CitizenCount <= 0) { enterprise.CitizenCount = Math.Max(1, source.EstimatedCitizenCount); } }

    private void ApplyImportedEnterpriseMetadata(Enterprise enterprise, EnterpriseReferenceSource source, string resolvedImportPath) { enterprise.Type = source.Type; enterprise.Description = Truncate($"Imported from QuickBooks reference data in {resolvedImportPath}.", 500); enterprise.Notes = Truncate(BuildEnterpriseNotes(source), 500); }

    private void LogImportSummary(string resolvedImportPath, EnterpriseImportSummary enterpriseImportSummary, UtilityCustomerImportSummary utilityCustomerImportSummary, LedgerImportSummary ledgerImportSummary, int discoveredCustomerRows) { logger.LogInformation("Imported workspace reference data from {ImportPath}: imported={Imported}, updated={Updated}, customerRows={CustomerRows}, importedCustomers={ImportedCustomers}, ledgerFiles={LedgerFiles}, ledgerRows={LedgerRows}, baselines={BaselineCount}", resolvedImportPath, enterpriseImportSummary.ImportedCount, enterpriseImportSummary.UpdatedCount, discoveredCustomerRows, utilityCustomerImportSummary.ImportedCount, ledgerImportSummary.ImportedFileCount, ledgerImportSummary.ImportedRowCount, enterpriseImportSummary.SeededBaselineCount); }

    private static WorkspaceReferenceDataImportResponse BuildImportResponse(string resolvedImportPath, DateTime importedAtUtc, IReadOnlyList<EnterpriseReferenceSource> sources, EnterpriseImportSummary enterpriseImportSummary, UtilityCustomerImportSummary utilityCustomerImportSummary, LedgerImportSummary ledgerImportSummary, int discoveredCustomerRows)
        => new WorkspaceReferenceDataImportResponse(resolvedImportPath, importedAtUtc.ToString("O", CultureInfo.InvariantCulture), enterpriseImportSummary.ImportedCount, enterpriseImportSummary.UpdatedCount, discoveredCustomerRows, utilityCustomerImportSummary.ImportedCount, utilityCustomerImportSummary.Status, sources.Select(item => item.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), ledgerImportSummary.ImportedFileCount, ledgerImportSummary.ImportedRowCount, ledgerImportSummary.Status, enterpriseImportSummary.SeededBaselineCount);

    private sealed class EnterpriseImportStats
    {
        public int ImportedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int SeededBaselineCount { get; set; }

        public EnterpriseImportSummary ToSummary()
            => new(ImportedCount, UpdatedCount, SeededBaselineCount);
    }

    private sealed record EnterpriseImportSummary(int ImportedCount, int UpdatedCount, int SeededBaselineCount);

    private sealed record ImportRequestContext(
        string ResolvedImportPath,
        IReadOnlyList<EnterpriseReferenceSource> Sources,
        DateTime ImportedAtUtc,
        bool ApplyDefaultEnterpriseBaselines);
}
