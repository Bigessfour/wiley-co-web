using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WileyCoWeb.Services;
using WileyCoWeb.State;
using Xunit;

namespace WileyCoWeb.ComponentTests.Services
{
    public class ExcelWorkbookBuilderTests
    {
        private readonly ExcelWorkbookBuilder _builder;

        public ExcelWorkbookBuilderTests()
        {
            _builder = new ExcelWorkbookBuilder();
        }

        [Fact]
        public void CreateCustomerWorkbook_WithValidData_CreatesWorkbook()
        {
            // Arrange
            var workspaceState = WorkspaceTestData.CreateWaterUtilityState();

            // Act
            var result = _builder.CreateCustomerWorkbook(workspaceState);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
            Assert.Contains("-customers.xlsx", result.FileName);
        }

        [Fact]
        public void CreateScenarioWorkbook_WithValidData_CreatesWorkbook()
        {
            // Arrange
            var workspaceState = WorkspaceTestData.CreateWaterUtilityState();

            // Act
            var result = _builder.CreateScenarioWorkbook(workspaceState);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
            Assert.Contains("-scenario.xlsx", result.FileName);
        }

        [Fact]
        public void CreateCustomerWorkbook_WithNullState_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _builder.CreateCustomerWorkbook(null!));
        }

        [Fact]
        public void CreateScenarioWorkbook_WithNullState_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _builder.CreateScenarioWorkbook(null!));
        }
    }
}