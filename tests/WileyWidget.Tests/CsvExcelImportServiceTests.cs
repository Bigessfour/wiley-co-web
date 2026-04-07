using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class CsvExcelImportServiceTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { });
    private readonly List<string> _createdFiles = new();

    [Fact]
    public async Task ImportTransactionsAsync_ReturnsFailure_WhenSameQuickBooksExportIsImportedTwice()
    {
        var databaseName = $"CsvExcelImportServiceTests-{Guid.NewGuid():N}";
        var service = CreateService(databaseName);
        var filePath = CreateTempCsvFile("Description,Amount,Date,Type,BudgetEntryId\nWater bill,12.34,2026-01-01,Debit,0\n");

        try
        {
            var firstResult = await service.ImportTransactionsAsync(filePath);
            Assert.True(firstResult.Success);
            Assert.Equal(1, firstResult.AccountsImported);

            var secondResult = await service.ImportTransactionsAsync(filePath);
            Assert.False(secondResult.Success);
            Assert.Contains("Duplicate import blocked", secondResult.ErrorMessage ?? string.Empty);

            var contextFactory = CreateContextFactory(databaseName);
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.SourceFiles.CountAsync());
        }
        finally
        {
            DeleteCreatedFiles();
        }
    }

    private CsvExcelImportService CreateService(string databaseName)
    {
        return new CsvExcelImportService(
            _loggerFactory.CreateLogger<CsvExcelImportService>(),
            CreateContextFactory(databaseName));
    }

    private static IDbContextFactory<AppDbContext> CreateContextFactory(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContextFactory(options);
    }

    private string CreateTempCsvFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(filePath, content);
        _createdFiles.Add(filePath);
        return filePath;
    }

    private void DeleteCreatedFiles()
    {
        foreach (var filePath in _createdFiles)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        _createdFiles.Clear();
    }

    public void Dispose()
    {
        DeleteCreatedFiles();
        _loggerFactory.Dispose();
    }
}
