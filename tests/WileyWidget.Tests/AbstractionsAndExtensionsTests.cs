using WileyWidget.Abstractions;
using WileyWidget.Services.Extensions;

namespace WileyWidget.Tests;

public sealed class AbstractionsAndExtensionsTests
{
    [Fact]
    public void Result_Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Result_Failure_CreatesFailedResultWithMessage()
    {
        var result = Result.Failure("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public void ResultOfT_Success_WithData_PreservesPayload()
    {
        var result = Result<string>.Success("alpha");

        Assert.True(result.IsSuccess);
        Assert.Equal("alpha", result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ResultOfT_Failure_WithException_AppendsExceptionMessage()
    {
        var exception = new InvalidOperationException("invalid state");

        var result = Result<string>.Failure("save failed", exception);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("save failed: invalid state", result.ErrorMessage);
    }

    [Fact]
    public void ResourceLoadResult_ToString_FormatsSummary()
    {
        var result = new ResourceLoadResult
        {
            Success = true,
            LoadedCount = 4,
            ErrorCount = 1,
            RetryCount = 2,
            LoadTimeMs = 450,
            HasCriticalFailures = false
        };

        Assert.Equal(
            "ResourceLoadResult: Success=True, Loaded=4, Errors=1, Retries=2, Time=450ms, CriticalFailures=False",
            result.ToString());
    }

    [Fact]
    public void ResourceLoadException_PreservesFailedResourcesAndCriticalFlag()
    {
        var exception = new ResourceLoadException(
            "load failed",
            new List<string> { "alpha.json", "beta.json" },
            new InvalidOperationException("inner"),
            isCritical: true);

        Assert.Equal("load failed", exception.Message);
        Assert.Equal(new[] { "alpha.json", "beta.json" }, exception.FailedResources);
        Assert.True(exception.IsCritical);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void IsEmpty_ReturnsTrue_ForNullAndEmptyCollections()
    {
        Assert.True(WileyWidget.Services.Extensions.CollectionExtensions.IsEmpty<int>(null!));
        Assert.True(Array.Empty<int>().IsEmpty());
        Assert.False(new[] { 1, 2, 3 }.IsEmpty());
    }

    [Fact]
    public void WhereNotNull_FiltersNullValues_AndAppliesPredicate()
    {
        var values = new List<string> { "alpha", null!, "beta", null!, "bravo" };

        var filtered = values.WhereNotNull(value => value.StartsWith("b", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Equal(new[] { "beta", "bravo" }, filtered);
    }
}