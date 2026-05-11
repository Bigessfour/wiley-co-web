using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class PdfPacketBuilder
{
    private const string PdfContentType = "application/pdf";
    private const int MaxNarrativeLength = 900;

    public WorkspaceExportDocument CreateWorkspacePdfReport(
        WorkspaceState workspaceState,
        string? scenarioNarrative = null,
        PersonnelPacketContext? personnelPacket = null)
    {
        return CreateWorkspacePdfReportCore(workspaceState, scenarioNarrative, personnelPacket);
    }

    private static WorkspaceExportDocument CreateWorkspacePdfReportCore(
        WorkspaceState workspaceState,
        string? scenarioNarrative,
        PersonnelPacketContext? personnelPacket)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var document = new PdfDocument();
        RenderWorkspaceRatePacket(document, workspaceState, NormalizeNarrative(scenarioNarrative), personnelPacket);

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-rate-packet.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static void RenderWorkspaceRatePacket(
        PdfDocument document,
        WorkspaceState workspaceState,
        string? scenarioNarrative,
        PersonnelPacketContext? personnelPacket)
    {
        var layout = CreatePacketLayout();
        var writer = new PdfPacketWriter(document, layout);

        WritePacketHeader(writer, workspaceState);
        WriteNarrativeSection(writer, scenarioNarrative);
        WriteSummarySection(writer, workspaceState);
        WriteRateVisualizationSection(writer, workspaceState);
        WriteScenarioItemsSection(writer, workspaceState);
        WritePersonnelSection(writer, personnelPacket);
        WriteProjectionSection(writer, workspaceState);
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

    private static void WritePacketHeader(PdfPacketWriter writer, WorkspaceState workspaceState)
    {
        writer.DrawText("TOWN OF WILEY, COLORADO | UTILITY RATE STUDY", writer.Layout.TitleFont, writer.Layout.Brush);
        writer.Advance(writer.Layout.LineHeight * 0.75f);
        writer.DrawText(workspaceState.ContextSummary, writer.Layout.SectionFont, writer.Layout.AccentBrush);
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

    private static void WriteSummarySection(PdfPacketWriter writer, WorkspaceState workspaceState)
    {
        writer.DrawSectionTitle("Financial summary");

        foreach (var line in BuildSummaryLines(workspaceState))
        {
            writer.DrawText(line, writer.Layout.BodyFont, writer.Layout.Brush);
        }

        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WriteRateVisualizationSection(PdfPacketWriter writer, WorkspaceState workspaceState)
    {
        writer.DrawSectionTitle("Rate comparison visualization");

        var current = workspaceState.CurrentRate;
        var adjusted = workspaceState.AdjustedRecommendedRate;
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
        writer.DrawText($"Scenario rate delta: {workspaceState.AdjustedRateDelta:C2}", writer.Layout.SmallFont, writer.Layout.Brush);
        writer.Advance(writer.Layout.LineHeight * 0.5f);
    }

    private static void WriteScenarioItemsSection(PdfPacketWriter writer, WorkspaceState workspaceState)
    {
        writer.DrawSectionTitle("Scenario items");

        if (workspaceState.ScenarioItems.Count == 0)
        {
            writer.DrawText("No scenario items are currently applied.", writer.Layout.BodyFont, writer.Layout.Brush);
            writer.Advance(writer.Layout.LineHeight * 0.5f);
            return;
        }

        var grid = CreateGrid(["Scenario item", "Annual cost", "Share"]);
        var scenarioTotal = workspaceState.ScenarioCostTotal;

        foreach (var item in workspaceState.ScenarioItems)
        {
            var row = grid.Rows.Add();
            row.Cells[0].Value = item.Name;
            row.Cells[1].Value = item.Cost.ToString("C0");
            row.Cells[2].Value = scenarioTotal == 0m ? "0%" : $"{item.Cost / scenarioTotal:P0}";
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

    private static void WriteProjectionSection(PdfPacketWriter writer, WorkspaceState workspaceState)
    {
        writer.DrawSectionTitle("Projection series");

        if (workspaceState.ProjectionSeries.Count == 0)
        {
            writer.DrawText("No projection series is currently available.", writer.Layout.BodyFont, writer.Layout.Brush);
            return;
        }

        var grid = CreateGrid(["Year", "Projected rate"]);

        foreach (var point in workspaceState.ProjectionSeries)
        {
            var row = grid.Rows.Add();
            row.Cells[0].Value = point.Year;
            row.Cells[1].Value = point.Rate.ToString("C2");
        }

        writer.DrawGrid(grid);
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

    private static IEnumerable<string> BuildSummaryLines(WorkspaceState workspaceState)
    {
        yield return $"Current rate: {workspaceState.CurrentRate:C2}";
        yield return $"Break-even rate: {workspaceState.RecommendedRate:C2}";
        yield return $"Scenario break-even rate: {workspaceState.AdjustedRecommendedRate:C2}";
        yield return $"Scenario rate delta: {workspaceState.AdjustedRateDelta:C2}";
        yield return $"Total costs: {workspaceState.TotalCosts:C0}";
        yield return $"Adjusted total costs: {workspaceState.AdjustedTotalCosts:C0}";
        yield return $"Projected volume: {workspaceState.ProjectedVolume:N0}";
        yield return $"Scenario cost total: {workspaceState.ScenarioCostTotal:C0}";
        yield return $"Visible customers: {workspaceState.FilteredCustomerCount}";
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

    private static string BuildFileStem(WorkspaceState workspaceState)
    {
        var enterprise = SanitizeFileName(workspaceState.SelectedEnterprise);
        return $"{enterprise}-fy{workspaceState.SelectedFiscalYear}";
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
}