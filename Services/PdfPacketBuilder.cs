using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class PdfPacketBuilder
{
    private const string PdfContentType = "application/pdf";

    public WorkspaceExportDocument CreateWorkspacePdfReport(WorkspaceState workspaceState)
    {
        return CreateWorkspacePdfReportCore(workspaceState);
    }

    public WorkspaceExportDocument CreateReserveTrajectoryPdfReport(WorkspaceState workspaceState)
    {
        return CreateReserveTrajectoryPdfReportCore(workspaceState);
    }

    private static WorkspaceExportDocument CreateWorkspacePdfReportCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var document = new PdfDocument();
        RenderWorkspaceRatePacket(document, workspaceState);

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-rate-packet.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static WorkspaceExportDocument CreateReserveTrajectoryPdfReportCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var document = new PdfDocument();
        RenderReserveTrajectoryReport(document, workspaceState);

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-reserve-trajectory.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static void RenderWorkspaceRatePacket(PdfDocument document, WorkspaceState workspaceState)
    {
        var page = document.Pages.Add();
        var graphics = page.Graphics;
        var layout = CreatePacketLayout();

        var y = WritePacketHeader(graphics, layout, workspaceState);
        y = WriteSummarySection(graphics, layout, workspaceState, y);
        y = WriteScenarioItemsSection(graphics, layout, workspaceState, y);
        WriteProjectionSection(graphics, layout, workspaceState, y);
    }

    private static void RenderReserveTrajectoryReport(PdfDocument document, WorkspaceState workspaceState)
    {
        var page = document.Pages.Add();
        var graphics = page.Graphics;
        var layout = CreatePacketLayout();

        var y = WriteReserveTrajectoryHeader(graphics, layout, workspaceState);
        y = WriteReserveTrajectorySummarySection(graphics, layout, workspaceState, y);
        WriteReserveTrajectoryForecastSection(graphics, layout, workspaceState, y);
    }

    private static PdfPacketLayout CreatePacketLayout()
    {
        return new PdfPacketLayout(
            new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold),
            new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold),
            new PdfStandardFont(PdfFontFamily.Helvetica, 10),
            new PdfSolidBrush(new PdfColor(15, 23, 42)),
            new PdfSolidBrush(new PdfColor(14, 116, 144)),
            36,
            32,
            18);
    }

    private static float WritePacketHeader(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState)
    {
        var y = layout.Top;
        graphics.DrawString("Wiley Workspace Rate Packet", layout.TitleFont, layout.Brush, new PointF(layout.Left, y));
        y += layout.LineHeight * 1.75f;
        graphics.DrawString(workspaceState.ContextSummary, layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        return y + layout.LineHeight * 1.5f;
    }

    private static float WriteReserveTrajectoryHeader(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState)
    {
        var y = layout.Top;
        graphics.DrawString("Wiley Workspace Reserve Trajectory", layout.TitleFont, layout.Brush, new PointF(layout.Left, y));
        y += layout.LineHeight * 1.75f;
        graphics.DrawString(workspaceState.ContextSummary, layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        return y + layout.LineHeight * 1.5f;
    }

    private static float WriteSummarySection(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState, float y)
    {
        foreach (var line in BuildSummaryLines(workspaceState))
        {
            graphics.DrawString(line, layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            y += layout.LineHeight;
        }

        return y + layout.LineHeight * 0.5f;
    }

    private static float WriteReserveTrajectorySummarySection(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState, float y)
    {
        var trajectory = workspaceState.ReserveTrajectory;

        graphics.DrawString("Overview", layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        y += layout.LineHeight;

        if (trajectory is null)
        {
            graphics.DrawString("Reserve trajectory data is not available yet.", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            return y + layout.LineHeight;
        }

        graphics.DrawString($"Current reserves: {trajectory.CurrentReserves:C0}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
        y += layout.LineHeight;
        graphics.DrawString($"Recommended reserve level: {trajectory.RecommendedReserveLevel:C0}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
        y += layout.LineHeight;
        graphics.DrawString($"Risk assessment: {trajectory.RiskAssessment}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
        y += layout.LineHeight;
        graphics.DrawString($"Forecast points: {trajectory.ForecastPoints.Count}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));

        return y + layout.LineHeight * 0.5f;
    }

    private static float WriteScenarioItemsSection(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState, float y)
    {
        graphics.DrawString("Scenario Items", layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        y += layout.LineHeight;

        if (workspaceState.ScenarioItems.Count == 0)
        {
            graphics.DrawString("No scenario items are currently applied.", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            return y + layout.LineHeight;
        }

        foreach (var item in workspaceState.ScenarioItems.Take(8))
        {
            graphics.DrawString($"- {item.Name}: {item.Cost:C0}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            y += layout.LineHeight;
        }

        return y + layout.LineHeight * 0.5f;
    }

    private static void WriteReserveTrajectoryForecastSection(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState, float y)
    {
        var trajectory = workspaceState.ReserveTrajectory;
        graphics.DrawString("Forecast", layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        y += layout.LineHeight;

        if (trajectory?.ForecastPoints is not { Count: > 0 })
        {
            graphics.DrawString("No reserve forecast points are currently available.", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            return;
        }

        foreach (var point in trajectory.ForecastPoints.Take(8))
        {
            graphics.DrawString($"- {point.DateUtc:MMM yy}: {point.PredictedReserves:C0} +/- {point.ConfidenceInterval:C0}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            y += layout.LineHeight;
        }
    }

    private static void WriteProjectionSection(PdfGraphics graphics, PdfPacketLayout layout, WorkspaceState workspaceState, float y)
    {
        graphics.DrawString("Projection Series", layout.SectionFont, layout.AccentBrush, new PointF(layout.Left, y));
        y += layout.LineHeight;

        foreach (var point in workspaceState.ProjectionSeries.Take(6))
        {
            graphics.DrawString($"- {point.Year}: {point.Rate:C2}", layout.BodyFont, layout.Brush, new PointF(layout.Left, y));
            y += layout.LineHeight;
        }
    }

    private sealed record PdfPacketLayout(
        PdfStandardFont TitleFont,
        PdfStandardFont SectionFont,
        PdfStandardFont BodyFont,
        PdfSolidBrush Brush,
        PdfSolidBrush AccentBrush,
        float Left,
        float Top,
        float LineHeight);

    private static IEnumerable<string> BuildSummaryLines(WorkspaceState workspaceState)
    {
        yield return $"Current rate: {workspaceState.CurrentRate:C2}";
        yield return $"Break-even rate: {workspaceState.RecommendedRate:C2}";
        yield return $"Adjusted break-even rate: {workspaceState.AdjustedRecommendedRate:C2}";
        yield return $"Projected volume: {workspaceState.ProjectedVolume:N0}";
        yield return $"Scenario pressure: {workspaceState.ScenarioCostTotal:C0}";
        yield return $"Visible customers: {workspaceState.FilteredCustomerCount}";
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