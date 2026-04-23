using Serilog;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class ReportExportServiceTests
{
    [Fact]
    public async Task ExportToCsvAsync_EscapesSpecialCharacters_AndCreatesOutputDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "exports", "report.csv");
        var service = new ReportExportService(new LoggerConfiguration().CreateLogger());

        try
        {
            await service.ExportToCsvAsync(
                [new { Name = "Ada, Jr.", Notes = "He said \"yes\", of course" }],
                outputPath);

            var content = await File.ReadAllTextAsync(outputPath);

            Assert.Contains("Name,Notes", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Ada, Jr.\"", content, StringComparison.Ordinal);
            Assert.Contains("\"He said \"\"yes\"\", of course\"", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task ExportComplianceReportToPdfAndExcelAsync_WritesExpectedFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pdfPath = Path.Combine(tempRoot, "reports", "compliance.pdf");
        var excelPath = Path.Combine(tempRoot, "reports", "compliance.xlsx");
        var service = new ReportExportService(new LoggerConfiguration().CreateLogger());
        var report = new ComplianceReport
        {
            EnterpriseId = 42,
            GeneratedDate = new DateTime(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc),
            OverallStatus = ComplianceStatus.Warning,
            ComplianceScore = 77,
            Violations =
            [
                new ComplianceViolation
                {
                    Regulation = "Water Code Section 10",
                    Description = new string('x', 120),
                    Severity = ViolationSeverity.High,
                    CorrectiveAction = new string('y', 120)
                }
            ],
            Recommendations =
            [
                new string('z', 120)
            ]
        };

        try
        {
            await service.ExportComplianceReportToPdfAsync(report, pdfPath);
            await service.ExportComplianceReportToExcelAsync(report, excelPath);

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            var excelBytes = await File.ReadAllBytesAsync(excelPath);

            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
            Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(excelBytes, 0, 2));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}