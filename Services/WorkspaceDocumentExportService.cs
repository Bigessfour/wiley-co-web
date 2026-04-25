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

    public WorkspaceExportDocument CreateWorkspacePdfReport(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);
        logger?.LogInformation("Creating PDF rate packet for {Enterprise} FY {FiscalYear} with {ProjectionPointCount} projection points.", workspaceState.SelectedEnterprise, workspaceState.SelectedFiscalYear, workspaceState.ProjectionSeries.Count);

        var document = _pdfBuilder.CreateWorkspacePdfReport(workspaceState);
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
}

public sealed record WorkspaceExportDocument(string FileName, string ContentType, byte[] Content);