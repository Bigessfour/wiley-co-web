#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Data;

/// <summary>
/// Persists analytics scenario snapshots to the application database.
/// </summary>
public sealed class ScenarioSnapshotRepository : IScenarioSnapshotRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ScenarioSnapshotRepository> _logger;

    public ScenarioSnapshotRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<ScenarioSnapshotRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SavedScenarioSnapshot> SaveAsync(SavedScenarioSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Name))
        {
            throw new ArgumentException("Scenario name is required.", nameof(snapshot));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var utcNow = DateTime.UtcNow;
        if (snapshot.CreatedAtUtc == default)
        {
            snapshot.CreatedAtUtc = utcNow;
        }

        snapshot.UpdatedAtUtc = utcNow;

        context.Set<SavedScenarioSnapshot>().Add(snapshot);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Saved scenario snapshot '{ScenarioName}' with Id {ScenarioId}", snapshot.Name, snapshot.Id);
        return snapshot;
    }

    public async Task<IReadOnlyList<SavedScenarioSnapshot>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var safeTake = take <= 0 ? 50 : take;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var results = await context.Set<SavedScenarioSnapshot>()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results;
    }
}
