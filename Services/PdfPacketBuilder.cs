using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class PdfPacketBuilder
{
    private const string PdfContentType = "application/pdf";

    // ── Page geometry ─────────────────────────────────────────────────────────
    private const float PageLeft      = 36f;
    private const float HeaderBandH   = 32f;
    private const float FooterBandH   = 24f;
    private const float ContentStartY = HeaderBandH + 12f;
    private const float LineH         = 18f;
    private const float SectionGap    = 10f;

    // ── Brand colours (Wiley slate-950 / cyan-700 palette) ───────────────────
    private static readonly PdfColor Navy  = new(15, 23, 42);
    private static readonly PdfColor Cyan  = new(14, 116, 144);
    private static readonly PdfColor Dark  = new(30, 41, 59);
    private static readonly PdfColor Muted = new(148, 163, 184);

    // ── Public API ────────────────────────────────────────────────────────────

    public WorkspaceExportDocument CreateWorkspacePdfReport(WorkspaceState workspaceState)
    {
        return CreateWorkspacePdfReportCore(workspaceState);
    }

    public WorkspaceExportDocument CreateReserveTrajectoryPdfReport(WorkspaceState workspaceState)
    {
        return CreateReserveTrajectoryPdfReportCore(workspaceState);
    }

    /// <summary>
    /// Builds a professional filename in the form
    /// <c>WileyCo-{label}-{enterprise}-FY{year}-Q{n}-{yyyy-MM-dd}.{ext}</c>.
    /// </summary>
    public static string BuildFileName(WorkspaceState workspaceState, string label, string extension)
    {
        var enterprise = SanitizeFileName(workspaceState.SelectedEnterprise);
        var now        = DateTimeOffset.UtcNow;
        var quarter    = $"Q{(now.Month - 1) / 3 + 1}";
        var date       = now.ToString("yyyy-MM-dd");
        return $"WileyCo-{label}-{enterprise}-FY{workspaceState.SelectedFiscalYear}-{quarter}-{date}.{extension}";
    }

    // ── Core builders ─────────────────────────────────────────────────────────

    private static WorkspaceExportDocument CreateWorkspacePdfReportCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var document = new PdfDocument();
        var state = new PageState(document, "RATE PACKET");

        state.DrawText("Wiley Workspace Rate Packet", state.Layout.TitleFont, new PdfSolidBrush(Dark));
        state.DrawText(workspaceState.ContextSummary, state.Layout.SectionFont, new PdfSolidBrush(Cyan));
        state.DrawText($"Generated: {DateTimeOffset.UtcNow:MMMM d, yyyy}", state.Layout.SmallFont, new PdfSolidBrush(Muted));
        state.Y += SectionGap;
        state.DrawDivider();

        RenderRateSection(state, workspaceState);
        RenderBreakEvenSection(state, workspaceState);
        RenderScenarioSection(state, workspaceState);
        RenderCustomerSection(state, workspaceState);
        RenderProjectionSection(state, workspaceState);
        RenderReserveSummarySection(state, workspaceState);

        ApplyPageNumbers(document, state.Layout);

        using var stream = new MemoryStream();
        document.Save(stream);
        return new WorkspaceExportDocument(
            BuildFileName(workspaceState, "Rate-Packet", "pdf"),
            PdfContentType,
            stream.ToArray());
    }

    private static WorkspaceExportDocument CreateReserveTrajectoryPdfReportCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var document = new PdfDocument();
        var state = new PageState(document, "RESERVE TRAJECTORY");

        state.DrawText("Wiley Workspace Reserve Trajectory", state.Layout.TitleFont, new PdfSolidBrush(Dark));
        state.DrawText(workspaceState.ContextSummary, state.Layout.SectionFont, new PdfSolidBrush(Cyan));
        state.DrawText($"Generated: {DateTimeOffset.UtcNow:MMMM d, yyyy}", state.Layout.SmallFont, new PdfSolidBrush(Muted));
        state.Y += SectionGap;
        state.DrawDivider();

        RenderReserveSummarySection(state, workspaceState);
        RenderReserveForecastSection(state, workspaceState);

        ApplyPageNumbers(document, state.Layout);

        using var stream = new MemoryStream();
        document.Save(stream);
        return new WorkspaceExportDocument(
            BuildFileName(workspaceState, "Reserve-Trajectory", "pdf"),
            PdfContentType,
            stream.ToArray());
    }

    // ── Section renderers ─────────────────────────────────────────────────────

    private static void RenderRateSection(PageState state, WorkspaceState ws)
    {
        state.EnsureSpace(SectionGap + LineH * 8);
        state.DrawSectionHeading("Rate Summary");
        WriteKV(state, "Current rate",        ws.CurrentRate.ToString("C2"));
        WriteKV(state, "Break-even rate",      ws.RecommendedRate.ToString("C2"));
        WriteKV(state, "Adjusted break-even",  ws.AdjustedRecommendedRate.ToString("C2"));
        WriteKV(state, "Rate delta",           ws.RateDelta.ToString("C2"));
        WriteKV(state, "Total costs",          ws.TotalCosts.ToString("C0"));
        WriteKV(state, "Projected volume",     ws.ProjectedVolume.ToString("N0"));
        WriteKV(state, "Scenario cost total",  ws.ScenarioCostTotal.ToString("C0"));
        state.Y += SectionGap;
    }

    private static void RenderBreakEvenSection(PageState state, WorkspaceState ws)
    {
        if (ws.BreakEvenQuadrants.Count == 0) return;

        state.EnsureSpace(SectionGap + LineH * 2);
        state.DrawSectionHeading("Break-Even Analysis");

        foreach (var q in ws.BreakEvenQuadrants)
        {
            state.EnsureSpace(LineH * 5);
            state.DrawBodyLine($"  {q.EnterpriseName}  ({q.EnterpriseType})", state.Layout.BoldFont);
            WriteKV(state, "    Break-even rate",  q.BreakEvenRate.ToString("C2"));
            WriteKV(state, "    Monthly balance",  q.MonthlyBalance.ToString("C0"));
            WriteKV(state, "    Eff. customers",   q.EffectiveCustomerCount.ToString("N0"));
            state.Y += SectionGap * 0.5f;
        }

        state.Y += SectionGap * 0.5f;
    }

    private static void RenderScenarioSection(PageState state, WorkspaceState ws)
    {
        state.EnsureSpace(SectionGap + LineH * 2);
        state.DrawSectionHeading("Scenario Items");

        if (ws.ScenarioItems.Count == 0)
        {
            state.DrawBodyLine("No scenario items are currently applied.");
        }
        else
        {
            foreach (var item in ws.ScenarioItems)
            {
                state.EnsureSpace(LineH);
                state.DrawBodyLine($"  {item.Name}  —  {item.Cost:C0}");
            }
        }

        state.Y += SectionGap;
    }

    private static void RenderCustomerSection(PageState state, WorkspaceState ws)
    {
        state.EnsureSpace(SectionGap + LineH * 5);
        state.DrawSectionHeading("Customer Summary");
        WriteKV(state, "Total customers",     ws.Customers.Count.ToString("N0"));
        WriteKV(state, "Filtered customers",  ws.FilteredCustomerCount.ToString("N0"));
        WriteKV(state, "Service filter",      ws.SelectedCustomerService);
        WriteKV(state, "City limits filter",  ws.SelectedCustomerCityLimits);
        state.Y += SectionGap;
    }

    private static void RenderProjectionSection(PageState state, WorkspaceState ws)
    {
        if (ws.ProjectionSeries.Count == 0) return;

        state.EnsureSpace(SectionGap + LineH * 2);
        state.DrawSectionHeading("Rate Projection Series");

        foreach (var point in ws.ProjectionSeries)
        {
            state.EnsureSpace(LineH);
            state.DrawBodyLine($"  {point.Year}:  {point.Rate:C2}");
        }

        state.Y += SectionGap;
    }

    private static void RenderReserveSummarySection(PageState state, WorkspaceState ws)
    {
        state.EnsureSpace(SectionGap + LineH * 5);
        state.DrawSectionHeading("Reserve Trajectory Summary");

        var trajectory = ws.ReserveTrajectory;
        if (trajectory is null)
        {
            state.DrawBodyLine("Reserve trajectory data is not available.");
            state.Y += SectionGap;
            return;
        }

        WriteKV(state, "Current reserves",           trajectory.CurrentReserves.ToString("C0"));
        WriteKV(state, "Recommended reserve level",  trajectory.RecommendedReserveLevel.ToString("C0"));
        WriteKV(state, "Risk assessment",            trajectory.RiskAssessment);
        WriteKV(state, "Forecast points",            trajectory.ForecastPoints.Count.ToString("N0"));
        state.Y += SectionGap;
    }

    private static void RenderReserveForecastSection(PageState state, WorkspaceState ws)
    {
        state.EnsureSpace(SectionGap + LineH * 2);
        state.DrawSectionHeading("Reserve Forecast");

        var trajectory = ws.ReserveTrajectory;
        if (trajectory?.ForecastPoints is not { Count: > 0 })
        {
            state.DrawBodyLine("No reserve forecast data is currently available.");
            return;
        }

        foreach (var point in trajectory.ForecastPoints)
        {
            state.EnsureSpace(LineH);
            state.DrawBodyLine($"  {point.DateUtc:MMM yy}:  {point.PredictedReserves:C0}  \u00b1{point.ConfidenceInterval:C0}");
        }
    }

    // ── Page layout helpers ───────────────────────────────────────────────────

    private static void WriteKV(PageState state, string key, string value)
    {
        state.EnsureSpace(LineH);
        state.Graphics.DrawString($"  {key}:", state.Layout.BodyFont, new PdfSolidBrush(Muted), new PointF(PageLeft, state.Y));
        state.Graphics.DrawString(value, state.Layout.BodyFont, new PdfSolidBrush(Dark), new PointF(PageLeft + 210f, state.Y));
        state.Y += LineH;
    }

    private static void ApplyPageNumbers(PdfDocument document, PacketLayout layout)
    {
        var total = document.Pages.Count;
        for (var i = 0; i < total; i++)
        {
            var page    = document.Pages[i];
            var g       = page.Graphics;
            var size    = page.GetClientSize();
            var footerY = size.Height - FooterBandH + 4f;

            g.DrawLine(
                new PdfPen(Muted, 0.5f),
                new PointF(PageLeft, footerY - 2f),
                new PointF(size.Width - PageLeft, footerY - 2f));

            g.DrawString(
                $"Page {i + 1} of {total}  |  Wiley Workspace \u2014 Confidential",
                layout.SmallFont,
                new PdfSolidBrush(Muted),
                new PointF(PageLeft, footerY));
        }
    }

    // ── File naming ───────────────────────────────────────────────────────────

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value
            .Trim()
            .Select(c => invalidChars.Contains(c) ? '-' : char.ToLowerInvariant(c))
            .ToArray())
            .Replace(' ', '-');
    }

    // ── Layout fonts record ───────────────────────────────────────────────────

    private sealed class PacketLayout
    {
        public readonly PdfStandardFont TitleFont   = new(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
        public readonly PdfStandardFont SectionFont = new(PdfFontFamily.Helvetica, 11, PdfFontStyle.Bold);
        public readonly PdfStandardFont BoldFont    = new(PdfFontFamily.Helvetica, 9,  PdfFontStyle.Bold);
        public readonly PdfStandardFont BodyFont    = new(PdfFontFamily.Helvetica, 9);
        public readonly PdfStandardFont SmallFont   = new(PdfFontFamily.Helvetica, 8);
    }

    // ── PageState: carries mutable current-page tracking state ───────────────

    private sealed class PageState
    {
        private readonly PdfDocument _document;
        private readonly string      _continuedLabel;
        private PdfPage              _currentPage;

        public readonly PacketLayout Layout = new();
        public float       Y        { get; set; }
        public PdfGraphics Graphics => _currentPage.Graphics;

        public PageState(PdfDocument document, string bandLabel)
        {
            _document       = document;
            _continuedLabel = $"{bandLabel} \u2014 CONTINUED";
            _currentPage    = document.Pages.Add();
            DrawBrandBand(bandLabel);
            Y = ContentStartY;
        }

        public void DrawBrandBand(string label)
        {
            var size = _currentPage.GetClientSize();
            Graphics.DrawRectangle(new PdfSolidBrush(Navy), new RectangleF(0, 0, size.Width, HeaderBandH));
            Graphics.DrawString(label, Layout.SmallFont, new PdfSolidBrush(Muted), new PointF(PageLeft, 9f));
        }

        public void DrawText(string text, PdfFont font, PdfBrush brush)
        {
            Graphics.DrawString(text, font, brush, new PointF(PageLeft, Y));
            Y += LineH;
        }

        public void DrawDivider()
        {
            var size = _currentPage.GetClientSize();
            Graphics.DrawLine(
                new PdfPen(Muted, 0.75f),
                new PointF(PageLeft, Y),
                new PointF(size.Width - PageLeft, Y));
            Y += 10f;
        }

        public void DrawSectionHeading(string heading)
        {
            Graphics.DrawRectangle(new PdfSolidBrush(Cyan), new RectangleF(PageLeft, Y + 1f, 3f, LineH - 2f));
            Graphics.DrawString(heading, Layout.SectionFont, new PdfSolidBrush(Cyan), new PointF(PageLeft + 8f, Y));
            Y += LineH + 2f;
        }

        public void DrawBodyLine(string text, PdfFont? font = null)
        {
            Graphics.DrawString(text, font ?? Layout.BodyFont, new PdfSolidBrush(Dark), new PointF(PageLeft, Y));
            Y += LineH;
        }

        public void EnsureSpace(float required)
        {
            var clientH = _currentPage.GetClientSize().Height;
            if (Y + required > clientH - FooterBandH - 10f)
                AddPage();
        }

        private void AddPage()
        {
            _currentPage = _document.Pages.Add();
            DrawBrandBand(_continuedLabel);
            Y = ContentStartY;
        }
    }
}