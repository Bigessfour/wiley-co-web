using System;
using System.Linq;
using WileyCoWeb.Services;
using WileyCoWeb.State;
using Xunit;

namespace WileyCoWeb.ComponentTests.Services
{
    public class PdfPacketBuilderTests
    {
        private readonly PdfPacketBuilder _builder;

        public PdfPacketBuilderTests()
        {
            _builder = new PdfPacketBuilder();
        }

        [Fact]
        public void CreateWorkspacePdfReport_WithValidData_CreatesPdf()
        {
            // Arrange
            var workspaceState = WorkspaceTestData.CreateWaterUtilityState();

            // Act
            var result = _builder.CreateWorkspacePdfReport(
                workspaceState,
                "Council review scenario for staffing and utility funding.");
            var pdfText = WorkspaceExportTestHelpers.ExtractPdfText(result.Content);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            Assert.Equal("application/pdf", result.ContentType);
            Assert.Contains("-rate-packet.pdf", result.FileName);
            Assert.Contains("TOWN OF WILEY, COLORADO | UTILITY RATE STUDY", pdfText);
            Assert.Contains("Powered by Wiley Widget + Semantic Kernel", pdfText);
            Assert.Contains("Council planning narrative", pdfText);
            Assert.Contains("Financial summary", pdfText);
            Assert.Contains("Rate comparison visualization", pdfText);
        }

        [Fact]
        public void CreateWorkspacePdfReport_WithNullState_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _builder.CreateWorkspacePdfReport(null!));
        }
    }
}