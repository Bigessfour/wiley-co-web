using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class FileImportServiceEdgeCaseTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { });

    private FileImportService CreateService() => new(_loggerFactory.CreateLogger<FileImportService>());

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_ForNullOrEmptyPath()
    {
        var service = CreateService();

        var nullResult = await service.ValidateImportFileAsync(null!);
        var emptyResult = await service.ValidateImportFileAsync(string.Empty);

        Assert.False(nullResult.IsSuccess);
        Assert.Contains("null or empty", nullResult.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.False(emptyResult.IsSuccess);
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_ForEmptyFile()
    {
        var service = CreateService();
        var path = CreateTempFile(".json", string.Empty);

        try
        {
            var result = await service.ValidateImportFileAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Contains("empty", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsFailure_ForOversizedFile()
    {
        var service = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            await using var stream = File.Create(path);
            stream.SetLength(100L * 1024 * 1024 + 1);

            var result = await service.ValidateImportFileAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Contains("maximum size", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_ForUnsupportedExtension()
    {
        var service = CreateService();
        var path = CreateTempFile(".txt", "alpha");

        try
        {
            var result = await service.ImportDataAsync<SampleJsonPayload>(path);

            Assert.False(result.IsSuccess);
            Assert.Contains("unsupported", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_ForInvalidJson()
    {
        var service = CreateService();
        var path = CreateTempFile(".json", "{ invalid json }");

        try
        {
            var result = await service.ImportDataAsync<SampleJsonPayload>(path);

            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid JSON format", result.ErrorMessage ?? string.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_ForInvalidXml()
    {
        var service = CreateService();
        var path = CreateTempFile(".xml", "<not-xml>");

        try
        {
            var result = await service.ImportDataAsync<SampleXmlPayload>(path);

            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid XML format", result.ErrorMessage ?? string.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ReturnsFailure_WhenCancelled()
    {
        var service = CreateService();
        var path = CreateTempFile(".json", "{\"name\":\"Alpha\"}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var result = await service.ImportDataAsync<SampleJsonPayload>(path, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Contains("cancelled", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportDataAsync_ParsesValidXmlPayload()
    {
        var service = CreateService();
        var path = CreateTempFile(".xml", "<SampleXmlPayload><Name>Bravo</Name></SampleXmlPayload>");

        try
        {
            var result = await service.ImportDataAsync<SampleXmlPayload>(path);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Bravo", result.Data!.Name);
        }
        finally
        {
            File.Delete(path);
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

}

public sealed class SampleJsonPayload
{
    public string? Name { get; set; }
}

public sealed class SampleXmlPayload
{
    public string? Name { get; set; }
}