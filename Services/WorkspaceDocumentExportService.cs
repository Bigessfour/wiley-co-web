using Microsoft.Extensions.Logging;
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
}

public sealed record WorkspaceExportDocument(string FileName, string ContentType, byte[] Content);