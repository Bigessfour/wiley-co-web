using System.Text.Json;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.XlsIO;
using WileyWidget.Abstractions;

namespace WileyWidget.Services;

public sealed class WorkspaceSnapshotExportArchiveService
{
    public const string CustomerWorkbookKind = "customer-workbook";
    public const string ScenarioWorkbookKind = "scenario-workbook";
    public const string WorkspacePdfKind = "workspace-pdf";

    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";
    private const string CurrencyFormat = "$#,##0.00";
    private const string RoundedCurrencyFormat = "$#,##0";
    private const string WholeNumberFormat = "#,##0";
    private const int MaxNarrativeLength = 900;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<WorkspaceSnapshotExportArtifactDocument> CreateDocuments(string snapshotPayload, IReadOnlyCollection<string>? requestedKinds = null)
    {
        if (string.IsNullOrWhiteSpace(snapshotPayload))
        {
            throw new ArgumentException("Snapshot payload is required.", nameof(snapshotPayload));
        }

        var snapshot = JsonSerializer.Deserialize<WorkspaceArchiveSnapshotData>(snapshotPayload, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot payload could not be deserialized for export generation.");

        var selectedKinds = NormalizeRequestedKinds(requestedKinds);
        var documents = new List<WorkspaceSnapshotExportArtifactDocument>(selectedKinds.Count);

        foreach (var kind in selectedKinds)
        {
            documents.Add(kind switch
            {
                CustomerWorkbookKind => CreateCustomerWorkbook(snapshot),
                ScenarioWorkbookKind => CreateScenarioWorkbook(snapshot),
                WorkspacePdfKind => CreateWorkspacePdfReport(snapshot),
                _ => throw new InvalidOperationException($"Unsupported export kind '{kind}'.")
            });
        }

        return documents;
    }

    private static IReadOnlyList<string> NormalizeRequestedKinds(IReadOnlyCollection<string>? requestedKinds)
    {
        if (requestedKinds == null || requestedKinds.Count == 0)
        {
            return [CustomerWorkbookKind, ScenarioWorkbookKind, WorkspacePdfKind];
        }

        var normalized = requestedKinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return [CustomerWorkbookKind, ScenarioWorkbookKind, WorkspacePdfKind];
        }

        return normalized;
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateCustomerWorkbook(WorkspaceArchiveSnapshotData snapshot)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(1);
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "Customers";

        WriteWorkbookTitle(worksheet, $"{snapshot.SelectedEnterprise} customer export", 1, 4);
        worksheet.Range[2, 1].Text = "Scenario";
        worksheet.Range[2, 2].Text = snapshot.ActiveScenarioName;
        worksheet.Range[3, 1].Text = "Fiscal Year";
        worksheet.Range[3, 2].Number = snapshot.SelectedFiscalYear;

        WriteHeaderRow(worksheet, 5, ["Name", "Service", "City Limits"]);

        var rowIndex = 6;
        foreach (var customer in snapshot.CustomerRows)
        {
            worksheet.Range[rowIndex, 1].Text = customer.Name;
            worksheet.Range[rowIndex, 2].Text = customer.Service;
            worksheet.Range[rowIndex, 3].Text = customer.CityLimits;
            rowIndex++;
        }

        worksheet.AutoFilters.FilterRange = worksheet.Range[5, 1, Math.Max(rowIndex - 1, 5), 3];
        worksheet.UsedRange.AutofitColumns();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            CustomerWorkbookKind,
            $"{BuildFileStem(snapshot)}-customers.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateScenarioWorkbook(WorkspaceArchiveSnapshotData snapshot)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(2);
        var summarySheet = workbook.Worksheets[0];
        summarySheet.Name = "Summary";
        WriteWorkbookTitle(summarySheet, $"{snapshot.SelectedEnterprise} rate summary", 1, 2);
        summarySheet.Range[3, 1].Text = "Current Rate";
        summarySheet.Range[3, 2].Number = (double)(snapshot.CurrentRate ?? 0m);
        summarySheet.Range[3, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[4, 1].Text = "Break-Even Rate";
        summarySheet.Range[4, 2].Number = (double)snapshot.RecommendedRate;
        summarySheet.Range[4, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[5, 1].Text = "Scenario Adjusted Rate";
        summarySheet.Range[5, 2].Number = (double)snapshot.AdjustedRecommendedRate;
        summarySheet.Range[5, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[6, 1].Text = "Scenario Cost Total";
        summarySheet.Range[6, 2].Number = (double)snapshot.ScenarioCostTotal;
        summarySheet.Range[6, 2].NumberFormat = RoundedCurrencyFormat;
        summarySheet.Range[7, 1].Text = "Projected Volume";
        summarySheet.Range[7, 2].Number = (double)(snapshot.ProjectedVolume ?? 0m);
        summarySheet.Range[7, 2].NumberFormat = WholeNumberFormat;
        summarySheet.UsedRange.AutofitColumns();

        var scenarioSheet = workbook.Worksheets[1];
        scenarioSheet.Name = "Scenario Items";
        WriteWorkbookTitle(scenarioSheet, snapshot.ContextSummary, 1, 3);
        WriteHeaderRow(scenarioSheet, 3, ["Scenario Item", "Cost", "Cost Delta vs Current Rate"]);

        var rowIndex = 4;
        foreach (var item in snapshot.ScenarioItems)
        {
            scenarioSheet.Range[rowIndex, 1].Text = item.Name;
            scenarioSheet.Range[rowIndex, 2].Number = (double)item.Cost;
            scenarioSheet.Range[rowIndex, 2].NumberFormat = RoundedCurrencyFormat;
            scenarioSheet.Range[rowIndex, 3].Number = (double)((snapshot.CurrentRate ?? 0m) - snapshot.AdjustedRecommendedRate);
            scenarioSheet.Range[rowIndex, 3].NumberFormat = CurrencyFormat;
            rowIndex++;
        }

        scenarioSheet.AutoFilters.FilterRange = scenarioSheet.Range[3, 1, Math.Max(rowIndex - 1, 3), 3];
        scenarioSheet.UsedRange.AutofitColumns();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            ScenarioWorkbookKind,
            $"{BuildFileStem(snapshot)}-scenario.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateWorkspacePdfReport(WorkspaceArchiveSnapshotData snapshot)
    {
        using var document = new PdfDocument();
        var layout = CreatePacketLayout();
        var writer = new PdfPacketWriter(document, layout);

        WritePacketHeader(writer, snapshot);
        WriteNarrativeSection(writer, NormalizeNarrative(snapshot.ScenarioDescription));
        WriteSummarySection(writer, snapshot);
        WriteRateVisualizationSection(writer, snapshot);
        WriteScenarioItemsSection(writer, snapshot);
        WritePersonnelSection(writer, CreatePersonnelPacketContext(snapshot));
        WriteProjectionSection(writer, snapshot);

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            WorkspacePdfKind,
            $"{BuildFileStem(snapshot)}-rate-packet.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static IEnumerable<string> BuildSummaryLines(WorkspaceArchiveSnapshotData snapshot)
    {
        yield return $"Current rate: {(snapshot.CurrentRate ?? 0m):C2}";
        yield return $"Break-even rate: {snapshot.RecommendedRate:C2}";
        yield return $"Scenario break-even rate: {snapshot.AdjustedRecommendedRate:C2}";
        yield return $"Scenario rate delta: {snapshot.AdjustedRateDelta:C2}";
        yield return $"Total costs: {(snapshot.TotalCosts ?? 0m):C0}";
        yield return $"Adjusted total costs: {snapshot.AdjustedTotalCosts:C0}";
        yield return $"Projected volume: {(snapshot.ProjectedVolume ?? 0m):N0}";
        yield return $"Scenario cost total: {snapshot.ScenarioCostTotal:C0}";
        yield return $"Visible customers: {snapshot.CustomerRows.Count}";
    }

    private static PdfPacketLayout CreatePacketLayout()
    {
        return new PdfPacketLayout(
            new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold),
            new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold),
            new PdfStandardFont(PdfFontFamily.Helvetica, 10),
            new PdfStandardFont(PdfFontFamily.Helvetica, 9),
            new PdfSolidBrush(new PdfColor(15, 23, 42)),
            new PdfSolidBrush(new PdfColor(14, 116, 144)),
            new PdfSolidBrush(new PdfColor(226, 232, 240)),
            new PdfSolidBrush(new PdfColor(56, 189, 248)),
            new PdfSolidBrush(new PdfColor(14, 165, 233)),
            36,
            32,
            36,
            18);
    }

    private static void WritePacketHeader(PdfPacketWriter writer, WorkspaceArchiveSnapshotData snapshot)
    {
        writer.DrawText("TOWN OF WILEY, COLORADO | UTILITY RATE STUDY", writer.Layout.TitleFont, writer.Layout.Brush);
        writer.Advance(writer.Layout.LineHeight * 0.75f);
        writer.DrawText(snapshot.ContextSummary, writer.Layout.SectionFont, writer.Layout.AccentBrush);
        writer.Advance(writer.Layout.LineHeight * 0.5f);
        writer.DrawText($"Generated {DateTimeOffset.Now:g}", writer.Layout.SmallFont, writer.Layout.Brush);
        writer.Advance(writer.Layout.LineHeight);
    }

    private static void WriteNarrativeSection(PdfPacketWriter writer, string? scenarioNarrative)
    {
        if (string.IsNullOrWhiteSpace(scenarioNarrative))
        {
            return;
        }

        writer.DrawSectionTitle("Council planning narrative");
        writer.DrawWrappedText(scenarioNarrative, 72);
        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WriteSummarySection(PdfPacketWriter writer, WorkspaceArchiveSnapshotData snapshot)
    {
        writer.DrawSectionTitle("Financial summary");

        foreach (var line in BuildSummaryLines(snapshot))
        {
            writer.DrawText(line, writer.Layout.BodyFont, writer.Layout.Brush);
        }

        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WriteRateVisualizationSection(PdfPacketWriter writer, WorkspaceArchiveSnapshotData snapshot)
    {
        writer.DrawSectionTitle("Rate comparison visualization");

        var current = snapshot.CurrentRate ?? 0m;
        var adjusted = snapshot.AdjustedRecommendedRate;
        var maxRate = Math.Max(Math.Max(current, adjusted), 1m);
        var labelWidth = 150f;
        var barWidth = writer.ContentWidth - labelWidth - 70f;
        var barHeight = 12f;
        var rowHeight = writer.Layout.LineHeight * 1.25f;

        writer.EnsureSpace(rowHeight * 2 + writer.Layout.LineHeight);
        DrawRateBar(writer, "Current rate", current, maxRate, labelWidth, barWidth, barHeight);
        writer.Advance(rowHeight);
        DrawRateBar(writer, "Scenario break-even", adjusted, maxRate, labelWidth, barWidth, barHeight);
        writer.Advance(rowHeight);
        writer.DrawText($"Scenario rate delta: {snapshot.AdjustedRateDelta:C2}", writer.Layout.SmallFont, writer.Layout.Brush);
        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WriteScenarioItemsSection(PdfPacketWriter writer, WorkspaceArchiveSnapshotData snapshot)
    {
        writer.DrawSectionTitle("Scenario items");

        if (snapshot.ScenarioItems.Count == 0)
        {
            writer.DrawText("No scenario items are currently applied.", writer.Layout.BodyFont, writer.Layout.Brush);
            writer.Advance(writer.Layout.LineHeight * 0.5f);
            return;
        }

        var grid = CreateGrid(["Scenario item", "Annual cost", "Share"]);

        foreach (var item in snapshot.ScenarioItems)
        {
            var row = grid.Rows.Add();
            row.Cells[0].Value = item.Name;
            row.Cells[1].Value = item.Cost.ToString("C0");
            row.Cells[2].Value = snapshot.ScenarioCostTotal == 0m ? "0%" : $"{item.Cost / snapshot.ScenarioCostTotal:P0}";
        }

        writer.DrawGrid(grid);
        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WritePersonnelSection(PdfPacketWriter writer, PersonnelPacketContext? personnelPacket)
    {
        if (personnelPacket is null)
        {
            return;
        }

        writer.DrawSectionTitle("New Personnel Cost Allocation");
        writer.DrawText("$25k Clerk: 25.0% split across Water, Sewer, Trash, and Apartments.", writer.Layout.BodyFont, writer.Layout.Brush);
        writer.DrawText("$55k Field Employee: 33.3% split across Water, Sewer, and Apartments.", writer.Layout.BodyFont, writer.Layout.Brush);
        var allocationGrid = CreateGrid(["Position", "Annual cost", "Allocation basis", "Water", "Sewer", "Trash", "Apartments"]);

        foreach (var line in personnelPacket.AllocationLines)
        {
            var row = allocationGrid.Rows.Add();
            row.Cells[0].Value = line.Position;
            row.Cells[1].Value = line.AnnualCost.ToString("C0");
            row.Cells[2].Value = line.AllocationBasis;
            row.Cells[3].Value = line.WaterAllocation.ToString("C0");
            row.Cells[4].Value = line.SewerAllocation.ToString("C0");
            row.Cells[5].Value = line.TrashAllocation.ToString("C0");
            row.Cells[6].Value = line.ApartmentsAllocation.ToString("C0");
        }

        writer.DrawGrid(allocationGrid);

        writer.DrawSectionTitle("Prorated annual cost per enterprise");
        var enterpriseGrid = CreateGrid(["Enterprise", "Prorated annual personnel cost"]);
        foreach (var allocation in personnelPacket.EnterpriseAllocations)
        {
            var row = enterpriseGrid.Rows.Add();
            row.Cells[0].Value = allocation.Enterprise;
            row.Cells[1].Value = allocation.AnnualCost.ToString("C0");
        }

        writer.DrawGrid(enterpriseGrid);

        writer.DrawSectionTitle("Rate Impact Summary");
        var rateGrid = CreateGrid(["Current rate", "Proposed rate", "% increase", "Current annual revenue", "New annual revenue"]);
        var rateRow = rateGrid.Rows.Add();
        rateRow.Cells[0].Value = personnelPacket.CurrentRate.ToString("C2");
        rateRow.Cells[1].Value = personnelPacket.ProposedRate.ToString("C2");
        rateRow.Cells[2].Value = personnelPacket.PercentIncrease.ToString("P1");
        rateRow.Cells[3].Value = personnelPacket.CurrentAnnualRevenue.ToString("C0");
        rateRow.Cells[4].Value = personnelPacket.ProposedAnnualRevenue.ToString("C0");
        writer.DrawGrid(rateGrid);

        writer.DrawInsightBox(personnelPacket.PlanningInsight);
    }

    private static void WriteProjectionSection(PdfPacketWriter writer, WorkspaceArchiveSnapshotData snapshot)
    {
        writer.DrawSectionTitle("Projection series");

        if (snapshot.ProjectionRows.Count == 0)
        {
            writer.DrawText("No projection series is currently available.", writer.Layout.BodyFont, writer.Layout.Brush);
            return;
        }

        var grid = CreateGrid(["Year", "Projected rate"]);

        foreach (var point in snapshot.ProjectionRows)
        {
            var row = grid.Rows.Add();
            row.Cells[0].Value = point.Year;
            row.Cells[1].Value = point.Rate.ToString("C2");
        }

        writer.DrawGrid(grid);
    }

    private static PdfGrid CreateGrid(IReadOnlyList<string> headers)
    {
        var grid = new PdfGrid
        {
            AllowRowBreakAcrossPages = false
        };

        grid.Columns.Add(headers.Count);
        grid.Headers.Add(1);

        var header = grid.Headers[0];
        for (var index = 0; index < headers.Count; index++)
        {
            header.Cells[index].Value = headers[index];
            header.Cells[index].Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(14, 116, 144));
            header.Cells[index].Style.TextBrush = PdfBrushes.White;
        }

        return grid;
    }

    private static void DrawRateBar(
        PdfPacketWriter writer,
        string label,
        decimal value,
        decimal maxRate,
        float labelWidth,
        float barWidth,
        float barHeight)
    {
        var layout = writer.Layout;
        var y = writer.Y;
        var labelPoint = new PointF(layout.Left, y);
        writer.Graphics.DrawString(label, layout.BodyFont, layout.Brush, labelPoint);

        var barX = layout.Left + labelWidth;
        var barY = y + 2;
        writer.Graphics.DrawRectangle(layout.MutedBrush, new RectangleF(barX, barY, barWidth, barHeight));

        var fillWidth = (float)(value / maxRate) * barWidth;
        var fillBrush = label.StartsWith("Current", StringComparison.Ordinal)
            ? layout.CurrentRateBrush
            : layout.ScenarioRateBrush;
        writer.Graphics.DrawRectangle(fillBrush, new RectangleF(barX, barY, fillWidth, barHeight));

        writer.Graphics.DrawString(value.ToString("C2"), layout.BodyFont, layout.Brush, new PointF(barX + barWidth + 10, y));
    }

    private static string? NormalizeNarrative(string? scenarioNarrative)
    {
        if (string.IsNullOrWhiteSpace(scenarioNarrative))
        {
            return null;
        }

        var normalized = scenarioNarrative.Trim();
        return normalized.Length <= MaxNarrativeLength
            ? normalized
            : $"{normalized[..MaxNarrativeLength]}...";
    }

    private sealed record PdfPacketLayout(
        PdfStandardFont TitleFont,
        PdfStandardFont SectionFont,
        PdfStandardFont BodyFont,
        PdfStandardFont SmallFont,
        PdfSolidBrush Brush,
        PdfSolidBrush AccentBrush,
        PdfSolidBrush MutedBrush,
        PdfSolidBrush CurrentRateBrush,
        PdfSolidBrush ScenarioRateBrush,
        float Left,
        float Top,
        float Right,
        float LineHeight);

    private sealed class PdfPacketWriter
    {
        private readonly PdfDocument document;

        public PdfPacketWriter(PdfDocument document, PdfPacketLayout layout)
        {
            this.document = document;
            Layout = layout;
            Page = document.Pages.Add();
            Graphics = Page.Graphics;
            Y = layout.Top;
            DrawFooter();
        }

        public PdfPacketLayout Layout { get; }
        public PdfPage Page { get; private set; }
        public PdfGraphics Graphics { get; private set; }
        public float Y { get; private set; }
        public float ContentWidth => Page.GetClientSize().Width - Layout.Left - Layout.Right;

        public void DrawSectionTitle(string title)
        {
            EnsureSpace(Layout.LineHeight * 2);
            Graphics.DrawString(title, Layout.SectionFont, Layout.AccentBrush, new PointF(Layout.Left, Y));
            Y += Layout.LineHeight * 1.25f;
        }

        public void DrawText(string text, PdfFont font, PdfBrush brush)
        {
            EnsureSpace(Layout.LineHeight);
            Graphics.DrawString(text, font, brush, new PointF(Layout.Left, Y));
            Y += Layout.LineHeight;
        }

        public void DrawWrappedText(string text, float height)
        {
            EnsureSpace(height + Layout.LineHeight);
            var bounds = new RectangleF(Layout.Left, Y, ContentWidth, height);
            var format = new PdfStringFormat
            {
                WordWrap = PdfWordWrapType.Word
            };
            Graphics.DrawString(text, Layout.BodyFont, Layout.Brush, bounds, format);
            Y += height + Layout.LineHeight * 0.5f;
        }

        public void DrawInsightBox(string text)
        {
            const float height = 78f;
            EnsureSpace(height + Layout.LineHeight);

            var bounds = new RectangleF(Layout.Left, Y, ContentWidth, height);
            Graphics.DrawRectangle(Layout.MutedBrush, bounds);
            Graphics.DrawRectangle(Layout.AccentBrush, new RectangleF(Layout.Left, Y, 4, height));

            var textBounds = new RectangleF(Layout.Left + 12, Y + 10, ContentWidth - 24, height - 20);
            var format = new PdfStringFormat
            {
                WordWrap = PdfWordWrapType.Word
            };
            Graphics.DrawString(text, Layout.BodyFont, Layout.Brush, textBounds, format);
            Y += height + Layout.LineHeight * 0.75f;
        }

        public void DrawGrid(PdfGrid grid)
        {
            EnsureSpace(Layout.LineHeight * 3);
            var format = new PdfGridLayoutFormat
            {
                Break = PdfLayoutBreakType.FitPage,
                Layout = PdfLayoutType.Paginate
            };
            var result = grid.Draw(Page, new PointF(Layout.Left, Y), format);
            Page = result.Page;
            Graphics = Page.Graphics;
            Y = result.Bounds.Bottom + Layout.LineHeight * 0.75f;
        }

        public void EnsureSpace(float height)
        {
            if (Y + height <= Page.GetClientSize().Height - Layout.Top)
            {
                return;
            }

            Page = document.Pages.Add();
            Graphics = Page.Graphics;
            Y = Layout.Top;
            DrawFooter();
        }

        public void Advance(float height)
        {
            EnsureSpace(height);
            Y += height;
        }

        private void DrawFooter()
        {
            var pageSize = Page.GetClientSize();
            var footerY = pageSize.Height - Layout.Top + Layout.LineHeight * 0.35f;
            Graphics.DrawString(
                "Powered by Wiley Widget + Semantic Kernel",
                Layout.SmallFont,
                Layout.AccentBrush,
                new PointF(Layout.Left, footerY));
        }
    }

    private static PersonnelPacketContext? CreatePersonnelPacketContext(WorkspaceArchiveSnapshotData snapshot)
    {
        var searchableParts = snapshot.ScenarioItems
            .Select(item => item.Name)
            .Append(snapshot.ActiveScenarioName)
            .Append(snapshot.ScenarioDescription ?? string.Empty);

        if (!searchableParts.Any(ContainsPersonnelKeyword))
        {
            return null;
        }

        return PersonnelPacketContext.Create(
            snapshot.CurrentRate ?? 0m,
            snapshot.AdjustedRecommendedRate,
            snapshot.ProjectedVolume ?? 0m);
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

    private static void WriteWorkbookTitle(IWorksheet worksheet, string title, int row, int columnSpan)
    {
        worksheet.Range[row, 1, row, columnSpan].Merge();
        worksheet.Range[row, 1].Text = title;
        worksheet.Range[row, 1].CellStyle.Font.Bold = true;
        worksheet.Range[row, 1].CellStyle.Font.Size = 16;
        worksheet.Range[row, 1].CellStyle.Color = Color.FromArgb(15, 23, 42);
        worksheet.Range[row, 1].CellStyle.Font.Color = ExcelKnownColors.White;
    }

    private static void WriteHeaderRow(IWorksheet worksheet, int rowIndex, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = worksheet.Range[rowIndex, index + 1];
            cell.Text = headers[index];
            cell.CellStyle.Font.Bold = true;
            cell.CellStyle.Color = Color.FromArgb(14, 116, 144);
            cell.CellStyle.Font.Color = ExcelKnownColors.White;
        }
    }

    private static string BuildFileStem(WorkspaceArchiveSnapshotData snapshot)
    {
        var enterprise = SanitizeFileName(snapshot.SelectedEnterprise);
        return $"{enterprise}-fy{snapshot.SelectedFiscalYear}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(character => invalidChars.Contains(character) ? '-' : char.ToLowerInvariant(character))
            .ToArray());

        return sanitized.Replace(' ', '-');
    }

    private sealed record PersonnelPacketContext(
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

    private sealed record PersonnelAllocationLine(
        string Position,
        decimal AnnualCost,
        string AllocationBasis,
        decimal WaterAllocation,
        decimal SewerAllocation,
        decimal TrashAllocation,
        decimal ApartmentsAllocation);

    private sealed record PersonnelEnterpriseAllocation(string Enterprise, decimal AnnualCost);

    private sealed record WorkspaceArchiveSnapshotData(
        string SelectedEnterprise,
        int SelectedFiscalYear,
        string ActiveScenarioName,
        decimal? CurrentRate,
        decimal? TotalCosts,
        decimal? ProjectedVolume)
    {
        public List<WorkspaceArchiveScenarioItemData> ScenarioItems { get; init; } = [];
        public List<WorkspaceArchiveCustomerRow> CustomerRows { get; init; } = [];
        public List<WorkspaceArchiveProjectionRow> ProjectionRows { get; init; } = [];
        public string? ScenarioDescription { get; init; }

        public decimal ScenarioCostTotal => ScenarioItems.Sum(item => item.Cost);
        public decimal AdjustedTotalCosts => (TotalCosts ?? 0m) + ScenarioCostTotal;
        public decimal RecommendedRate => EnterpriseRateService.CalculateBreakEvenRate(TotalCosts ?? 0m, ProjectedVolume ?? 0m);
        public decimal AdjustedRecommendedRate => EnterpriseRateService.CalculateAdjustedBreakEvenRate(
            TotalCosts ?? 0m,
            ScenarioCostTotal,
            ProjectedVolume ?? 0m);
        public decimal AdjustedRateDelta => EnterpriseRateService.CalculateAdjustedRateDelta(CurrentRate ?? 0m, AdjustedRecommendedRate);
        public string ContextSummary => $"{SelectedEnterprise} FY {SelectedFiscalYear} | {ActiveScenarioName}";
    }

    private sealed record WorkspaceArchiveScenarioItemData(Guid Id, string Name, decimal Cost);
    private sealed record WorkspaceArchiveCustomerRow(string Name, string Service, string CityLimits);
    private sealed record WorkspaceArchiveProjectionRow(string Year, decimal Rate);
}

public sealed record WorkspaceSnapshotExportArtifactDocument(string DocumentKind, string FileName, string ContentType, byte[] Content);