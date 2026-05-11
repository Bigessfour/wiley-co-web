using Microsoft.Extensions.Logging;
using System.IO.Compression;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceDocumentExportService(
    ILogger<WorkspaceDocumentExportService>? logger = null,
    ExcelWorkbookBuilder? excelBuilder = null,
    PdfPacketBuilder? pdfBuilder = null)
{
    private readonly ExcelWorkbookBuilder _excelBuilder = excelBuilder ?? new ExcelWorkbookBuilder();
    private readonly PdfPacketBuilder _pdfBuilder = pdfBuilder ?? new PdfPacketBuilder();

    public WorkspaceExportDocument CreateCustomerWorkbook(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating customer workbook export for {Enterprise} FY {FiscalYear} with {CustomerCount} customers.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.FilteredCustomerCount);

        var document = _excelBuilder.CreateCustomerWorkbook(workspaceState);
        logger?.LogInformation("Customer workbook export created: {FileName} ({ByteCount} bytes)", document.FileName, document.Content.LongLength);
        return document;
    }

    public WorkspaceExportDocument CreateScenarioWorkbook(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating scenario workbook export for {Enterprise} FY {FiscalYear} with {ScenarioItemCount} scenario items.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.ScenarioItems.Count);

        var document = _excelBuilder.CreateScenarioWorkbook(workspaceState);
        logger?.LogInformation("Scenario workbook export created: {FileName} ({ByteCount} bytes)", document.FileName, document.Content.LongLength);
        return document;
    }

    public WorkspaceExportDocument CreateReserveTrajectoryWorkbook(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating reserve trajectory workbook export for {Enterprise} FY {FiscalYear} with {ForecastPointCount} forecast points.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.ReserveTrajectory?.ForecastPoints.Count ?? 0);

        var document = _excelBuilder.CreateReserveTrajectoryWorkbook(workspaceState);
        logger?.LogInformation("Reserve trajectory workbook export created: {FileName} ({ByteCount} bytes)", document.FileName, document.Content.LongLength);
        return document;
    }

    public WorkspaceExportDocument CreateWorkspacePdfReport(WorkspaceState workspaceState, string? scenarioNarrative = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating PDF rate packet for {Enterprise} FY {FiscalYear} with {ProjectionPointCount} projection points.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.ProjectionSeries.Count);

        var personnelPacket = CreatePersonnelPacketContext(workspaceState, scenarioNarrative);
        var document = _pdfBuilder.CreateWorkspacePdfReport(workspaceState, scenarioNarrative, personnelPacket);
        logger?.LogInformation("PDF rate packet created: {FileName} ({ByteCount} bytes)", document.FileName, document.Content.LongLength);
        return document;
    }

    public WorkspaceExportDocument CreateReserveTrajectoryPdfReport(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating reserve trajectory PDF export for {Enterprise} FY {FiscalYear} with {ForecastPointCount} forecast points.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.ReserveTrajectory?.ForecastPoints.Count ?? 0);

        var document = _pdfBuilder.CreateReserveTrajectoryPdfReport(workspaceState);
        logger?.LogInformation("Reserve trajectory PDF export created: {FileName} ({ByteCount} bytes)", document.FileName, document.Content.LongLength);
        return document;
    }

    public WorkspaceExportDocument CreateRatePacketZip(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating rate packet ZIP for {Enterprise} FY {FiscalYear}.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear);

        var pdfDoc   = _pdfBuilder.CreateWorkspacePdfReport(workspaceState);
        var excelDoc = _excelBuilder.CreateRatePacketWorkbook(workspaceState);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var pdfEntry = archive.CreateEntry(pdfDoc.FileName, CompressionLevel.Optimal);
            using (var s = pdfEntry.Open()) s.Write(pdfDoc.Content);

            var xlsxEntry = archive.CreateEntry(excelDoc.FileName, CompressionLevel.Optimal);
            using (var s = xlsxEntry.Open()) s.Write(excelDoc.Content);
        }

        var zipFileName = PdfPacketBuilder.BuildFileName(workspaceState, "Rate-Packet", "zip");
        logger?.LogInformation("Rate packet ZIP created: {FileName} ({ByteCount} bytes)", zipFileName, zipStream.Length);
        return new WorkspaceExportDocument(zipFileName, "application/zip", zipStream.ToArray());
    }

    public static bool ContainsPersonnelScenario(WorkspaceState workspaceState, string? scenarioNarrative = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        return CreatePersonnelPacketContext(workspaceState, scenarioNarrative) is not null;
    }

    private static PersonnelPacketContext? CreatePersonnelPacketContext(WorkspaceState workspaceState, string? scenarioNarrative)
    {
        var searchableParts = workspaceState.ScenarioItems
            .Select(item => item.Name)
            .Append(workspaceState.ActiveScenarioName)
            .Append(scenarioNarrative ?? string.Empty);

        if (!searchableParts.Any(ContainsPersonnelKeyword))
        {
            return null;
        }

        return PersonnelPacketContext.Create(
            workspaceState.CurrentRate,
            workspaceState.AdjustedRecommendedRate,
            workspaceState.ProjectedVolume);
    }

    private static bool ContainsPersonnelKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("personnel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("hire", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clerk", StringComparison.OrdinalIgnoreCase)
            || value.Contains("field employee", StringComparison.OrdinalIgnoreCase)
            || value.Contains("field staff", StringComparison.OrdinalIgnoreCase)
            || value.Contains("staffing", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record WorkspaceExportDocument(string FileName, string ContentType, byte[] Content);

public sealed record PersonnelPacketContext(
    IReadOnlyList<PersonnelAllocationLine> AllocationLines,
    IReadOnlyList<PersonnelEnterpriseAllocation> EnterpriseAllocations,
    decimal CurrentRate,
    decimal ProposedRate,
    decimal PercentIncrease,
    decimal CurrentAnnualRevenue,
    decimal ProposedAnnualRevenue,
    string PlanningInsight)
{
    public static PersonnelPacketContext Create(decimal currentRate, decimal proposedRate, decimal projectedVolume)
    {
        const decimal clerkAnnualCost = 25000m;
        const decimal fieldEmployeeAnnualCost = 55000m;
        const decimal clerkShare = 0.25m;
        const decimal fieldShare = 1m / 3m;

        var allocationLines = new List<PersonnelAllocationLine>
        {
            new("PT City Clerk", clerkAnnualCost, "25.0% split across Water, Sewer, Trash, and Apartments", clerkAnnualCost * clerkShare, clerkAnnualCost * clerkShare, clerkAnnualCost * clerkShare, clerkAnnualCost * clerkShare),
            new("FT Field Employee", fieldEmployeeAnnualCost, "33.3% split across Water, Sewer, and Apartments", fieldEmployeeAnnualCost * fieldShare, fieldEmployeeAnnualCost * fieldShare, 0m, fieldEmployeeAnnualCost * fieldShare)
        };

        var enterpriseAllocations = new List<PersonnelEnterpriseAllocation>
        {
            new("Water", allocationLines.Sum(line => line.WaterAllocation)),
            new("Sewer", allocationLines.Sum(line => line.SewerAllocation)),
            new("Trash", allocationLines.Sum(line => line.TrashAllocation)),
            new("Apartments", allocationLines.Sum(line => line.ApartmentsAllocation))
        };

        var currentAnnualRevenue = currentRate * projectedVolume;
        var proposedAnnualRevenue = proposedRate * projectedVolume;
        var percentIncrease = currentRate == 0m ? 0m : (proposedRate - currentRate) / currentRate;
        var planningInsight =
            "Wiley Widget/Semantic Kernel planning insight: the allocation keeps the part-time clerk spread evenly across all four enterprises while assigning the field employee to Water, Sewer, and Apartments where the operating burden is concentrated. Funding these shares through the proposed rate preserves reserve capacity by matching recurring personnel expense to recurring rate revenue instead of drawing down one-time balances.";

        return new PersonnelPacketContext(
            allocationLines,
            enterpriseAllocations,
            currentRate,
            proposedRate,
            percentIncrease,
            currentAnnualRevenue,
            proposedAnnualRevenue,
            planningInsight);
    }
}

public sealed record PersonnelAllocationLine(
    string Position,
    decimal AnnualCost,
    string AllocationBasis,
    decimal WaterAllocation,
    decimal SewerAllocation,
    decimal TrashAllocation,
    decimal ApartmentsAllocation);

public sealed record PersonnelEnterpriseAllocation(string Enterprise, decimal AnnualCost);