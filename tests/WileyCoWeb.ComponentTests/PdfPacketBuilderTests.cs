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
            var result = _builder.CreateWorkspacePdfReport(workspaceState);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            Assert.Equal("application/pdf", result.ContentType);
            Assert.Contains("Rate-Packet", result.FileName);
            Assert.EndsWith(".pdf", result.FileName);
        }

        [Fact]
        public void CreateWorkspacePdfReport_WithNullState_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _builder.CreateWorkspacePdfReport(null!));
        }
    }
}