using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class FileImportServiceTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { });

    private FileImportService CreateService()
    {
        return new FileImportService(_loggerFactory.CreateLogger<FileImportService>());
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_WhenPathIsEmpty()
    {
        var service = CreateService();

        var result = await service.ValidateImportFileAsync(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Contains("null or empty", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_WhenFileDoesNotExist()
    {
        var service = CreateService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        var result = await service.ValidateImportFileAsync(missingPath);

        Assert.False(result.IsSuccess);
        Assert.Contains("File not found", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_WhenFileIsEmpty()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".txt", string.Empty);

        try
        {
            var result = await service.ValidateImportFileAsync(filePath);

            Assert.False(result.IsSuccess);
            Assert.Contains("File is empty", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsSuccess_ForExistingNonEmptyFile()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".txt", "hello world");

        try
        {
            var result = await service.ValidateImportFileAsync(filePath);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ParsesJsonPayload()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".json", "{\"name\":\"Alpha\"}");

        try
        {
            var result = await service.ImportDataAsync<SamplePayload>(filePath);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Alpha", result.Data!.Name);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_ForInvalidJson()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".json", "{\"name\":");

        try
        {
            var result = await service.ImportDataAsync<SamplePayload>(filePath);

            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid JSON format", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_ForUnsupportedCsvFormat()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".csv", "name\nAlpha\n");

        try
        {
            var result = await service.ImportDataAsync<SamplePayload>(filePath);

            Assert.False(result.IsSuccess);
            Assert.Contains("not yet supported", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_WhenCancellationIsRequested()
    {
        var service = CreateService();
        var filePath = CreateTempFile(".json", "{\"name\":\"Alpha\"}");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        try
        {
            var result = await service.ImportDataAsync<SamplePayload>(filePath, cancellationTokenSource.Token);

            Assert.False(result.IsSuccess);
            Assert.Contains("cancelled", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string CreateTempFile(string extension, string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    private sealed class SamplePayload
    {
        public string? Name { get; set; }
    }
}