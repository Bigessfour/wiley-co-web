using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Abstractions;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class MemoryCacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task SetGetExistsAndRemove_WorkForCachedObjects()
    {
        var service = new MemoryCacheService(_memoryCache);
        var payload = new SampleCacheItem
        {
            Name = "alpha",
            Count = 1,
            Tags = new List<string> { "one", "two" }
        };

        await service.SetAsync("sample:key", payload, ttl: TimeSpan.FromMinutes(1));

        Assert.True(await service.ExistsAsync("sample:key"));

        var cached = await service.GetAsync<SampleCacheItem>("sample:key");

        Assert.NotNull(cached);
        Assert.NotSame(payload, cached);
        Assert.Equal("alpha", cached!.Name);

        cached.Name = "changed";

        var cachedAgain = await service.GetAsync<SampleCacheItem>("sample:key");

        Assert.Equal("alpha", cachedAgain!.Name);

        await service.RemoveAsync("sample:key");

        Assert.False(await service.ExistsAsync("sample:key"));
        Assert.Null(await service.GetAsync<SampleCacheItem>("sample:key"));
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsExistingValue_WithoutInvokingFactory()
    {
        var service = new MemoryCacheService(_memoryCache);
        var payload = new SampleCacheItem { Name = "cached" };
        await service.SetAsync("sample:key", payload, ttl: TimeSpan.FromMinutes(1));

        var calls = 0;

        var result = await service.GetOrCreateAsync("sample:key", () =>
        {
            calls++;
            return Task.FromResult(new SampleCacheItem { Name = "fresh" });
        });

        Assert.Equal(0, calls);
        Assert.Equal("cached", result.Name);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult_WithCustomOptions()
    {
        var service = new MemoryCacheService(_memoryCache);
        var options = new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Size = 3,
            Priority = 2,
        };

        var result = await service.GetOrCreateAsync("sample:options", () => Task.FromResult(new SampleCacheItem { Name = "created" }), options);

        Assert.Equal("created", result.Name);
        Assert.True(await service.ExistsAsync("sample:options"));

        await service.RemoveAsync("sample:options");
    }

    [Fact]
    public async Task ClearAllAsync_RemovesAllEntries()
    {
        var service = new MemoryCacheService(_memoryCache);
        await service.SetAsync("sample:one", new SampleCacheItem { Name = "one" }, ttl: TimeSpan.FromMinutes(1));
        await service.SetAsync("sample:two", new SampleCacheItem { Name = "two" }, ttl: TimeSpan.FromMinutes(1));

        await service.ClearAllAsync();

        Assert.False(await service.ExistsAsync("sample:one"));
        Assert.False(await service.ExistsAsync("sample:two"));
    }

    [Fact]
    public async Task NullOrEmptyKeys_AreHandledSafely()
    {
        var service = new MemoryCacheService(_memoryCache);

        Assert.Null(await service.GetAsync<SampleCacheItem>(string.Empty));
        Assert.False(await service.ExistsAsync(null!));
        await service.SetAsync<string>(null!, "value", ttl: TimeSpan.FromMinutes(1));
        await service.SetAsync("sample:null", (SampleCacheItem)null!, ttl: TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    private sealed class SampleCacheItem
    {
        public string? Name { get; set; }

        public int Count { get; set; }

        public List<string> Tags { get; set; } = new();
    }
}