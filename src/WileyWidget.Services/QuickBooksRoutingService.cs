using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models.Amplify;

namespace WileyWidget.Services;

public sealed class QuickBooksRoutingService
{
    private const string QuickBooksCanonicalEntity = "quickbooks-ledger";

    private static readonly (string Pattern, string EnterpriseName)[] DefaultEnterpriseMappings =
    [
        ("WSD", "Wiley Sanitation District"),
        ("sanitation", "Wiley Sanitation District"),
        ("sewer", "Wiley Sanitation District"),
        ("Util", "Water Utility"),
        ("utility", "Water Utility")
    ];

    private static readonly HashSet<string> GenericScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "general_ledger",
        "transaction_list"
    };

    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public QuickBooksRoutingService(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<QuickBooksRoutingConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await BuildConfigurationResponseAsync(context, "Loaded QuickBooks routing configuration.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<QuickBooksRoutingConfigurationResponse> SaveConfigurationAsync(QuickBooksRoutingConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConfiguration(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.QuickBooksRoutingRules.RemoveRange(context.QuickBooksRoutingRules);
        context.QuickBooksAllocationTargets.RemoveRange(context.QuickBooksAllocationTargets);
        context.QuickBooksAllocationProfiles.RemoveRange(context.QuickBooksAllocationProfiles);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var profileIdMap = new Dictionary<long, QuickBooksAllocationProfile>();

        foreach (var profile in request.AllocationProfiles.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var profileEntity = new QuickBooksAllocationProfile
            {
                Name = profile.Name.Trim(),
                Description = NullIfWhiteSpace(profile.Description),
                IsActive = profile.IsActive
            };

            foreach (var target in profile.Targets.Where(item => !string.IsNullOrWhiteSpace(item.EnterpriseName)))
            {
                profileEntity.Targets.Add(new QuickBooksAllocationTarget
                {
                    EnterpriseName = target.EnterpriseName.Trim(),
                    AllocationPercent = decimal.Round(target.AllocationPercent, 2, MidpointRounding.AwayFromZero)
                });
            }

            context.QuickBooksAllocationProfiles.Add(profileEntity);
            profileIdMap[profile.Id] = profileEntity;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var rule in request.Rules.OrderBy(item => item.Priority).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            context.QuickBooksRoutingRules.Add(new QuickBooksRoutingRule
            {
                Name = rule.Name.Trim(),
                Description = NullIfWhiteSpace(rule.Description),
                Priority = rule.Priority,
                IsActive = rule.IsActive,
                SourceFilePattern = NullIfWhiteSpace(rule.SourceFilePattern),
                DefaultEnterprisePattern = NullIfWhiteSpace(rule.DefaultEnterprisePattern),
                AccountPattern = NullIfWhiteSpace(rule.AccountPattern),
                MemoPattern = NullIfWhiteSpace(rule.MemoPattern),
                NamePattern = NullIfWhiteSpace(rule.NamePattern),
                SplitAccountPattern = NullIfWhiteSpace(rule.SplitAccountPattern),
                TargetEnterprise = NullIfWhiteSpace(rule.TargetEnterprise),
                AllocationProfileId = rule.AllocationProfileId is { } profileId && profileIdMap.TryGetValue(profileId, out var mappedProfile)
                    ? mappedProfile.Id
                    : null
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildConfigurationResponseAsync(context, "Saved QuickBooks routing configuration.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QuickBooksImportPreviewRow>> ApplyRoutingAsync(
        IReadOnlyList<QuickBooksImportPreviewRow> rows,
        string fileName,
        string selectedEnterprise,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var configuration = await LoadConfigurationAsync(context, cancellationToken).ConfigureAwait(false);
        return ApplyRouting(rows, fileName, selectedEnterprise, configuration);
    }

    public async Task<QuickBooksImportHistoryResponse> GetImportHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var sourceFiles = await context.SourceFiles
            .AsNoTracking()
            .Include(item => item.LedgerEntries)
            .Where(item => item.CanonicalEntity == QuickBooksCanonicalEntity)
            .OrderByDescending(item => item.ImportedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = sourceFiles
            .Select(item => new QuickBooksImportHistoryItem
            {
                SourceFileId = item.Id,
                BatchId = item.BatchId,
                FileName = item.OriginalFileName,
                ScopeSummary = BuildScopeSummary(item.LedgerEntries),
                RowCount = item.RowCount,
                ImportedAtUtc = item.ImportedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
            })
            .ToList();

        return new QuickBooksImportHistoryResponse
        {
            Items = items,
            StatusMessage = items.Count == 0
                ? "No QuickBooks import history is available yet."
                : $"Loaded {items.Count} QuickBooks import history item(s)."
        };
    }

    public async Task<QuickBooksHistoricalRerouteResponse> ReapplyRoutingAsync(QuickBooksHistoricalRerouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sourceFile = await context.SourceFiles
            .Include(item => item.LedgerEntries)
            .FirstOrDefaultAsync(item => item.Id == request.SourceFileId && item.CanonicalEntity == QuickBooksCanonicalEntity, cancellationToken)
            .ConfigureAwait(false);

        if (sourceFile is null)
        {
            throw new InvalidOperationException("The selected QuickBooks source file could not be found.");
        }

        var rawRows = RebuildRawRows(sourceFile.LedgerEntries);
        var configuration = await LoadConfigurationAsync(context, cancellationToken).ConfigureAwait(false);
        var defaultEnterprise = ResolveHistoricalDefaultEnterprise(sourceFile.OriginalFileName, sourceFile.LedgerEntries);
        var routedRows = ApplyRouting(rawRows, sourceFile.OriginalFileName, defaultEnterprise, configuration);

        context.LedgerEntries.RemoveRange(sourceFile.LedgerEntries);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var routedRow in routedRows)
        {
            context.LedgerEntries.Add(CreateLedgerEntry(sourceFile.Id, routedRow, defaultEnterprise));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new QuickBooksHistoricalRerouteResponse
        {
            SourceFileId = sourceFile.Id,
            FileName = sourceFile.OriginalFileName,
            SourceRowCount = rawRows.Count,
            RoutedRowCount = routedRows.Count,
            StatusMessage = $"Reapplied QuickBooks routing to {sourceFile.OriginalFileName}: {rawRows.Count} source row(s) produced {routedRows.Count} routed row(s)."
        };
    }

    public LedgerEntry CreateLedgerEntry(long sourceFileId, QuickBooksImportPreviewRow row, string defaultEnterprise)
    {
        return new LedgerEntry
        {
            SourceFileId = sourceFileId,
            SourceRowNumber = row.RowNumber,
            EntryDate = TryParseDateOnly(row.EntryDate),
            EntryType = row.EntryType,
            TransactionNumber = row.TransactionNumber,
            Name = row.Name,
            Memo = row.Memo,
            AccountName = row.AccountName,
            SplitAccount = row.SplitAccount,
            Amount = row.Amount,
            RunningBalance = row.RunningBalance,
            ClearedFlag = row.ClearedFlag,
            EntryScope = string.IsNullOrWhiteSpace(row.RoutedEnterprise) ? defaultEnterprise : row.RoutedEnterprise!,
            OriginalEntryScope = defaultEnterprise,
            SourceAmount = row.SourceAmount ?? row.Amount,
            AppliedRoutingRuleName = NullIfWhiteSpace(row.RoutingRuleName),
            AppliedAllocationProfileName = TryExtractAllocationProfileName(row.AllocationSummary),
            RoutingAllocationPercent = TryExtractAllocationPercent(row.AllocationSummary),
            RoutingReason = NullIfWhiteSpace(row.RoutingReason)
        };
    }

    private static DateOnly? TryParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : null;
    }

    private static void ValidateConfiguration(QuickBooksRoutingConfigurationRequest request)
    {
        foreach (var profile in request.AllocationProfiles)
        {
            var totalPercent = profile.Targets.Sum(item => item.AllocationPercent);
            if (totalPercent > 100.00m)
            {
                throw new InvalidOperationException($"Allocation profile '{profile.Name}' exceeds 100%.");
            }
        }
    }

    private async Task<QuickBooksRoutingConfigurationResponse> BuildConfigurationResponseAsync(AppDbContext context, string statusMessage, CancellationToken cancellationToken)
    {
        var configuration = await LoadConfigurationAsync(context, cancellationToken).ConfigureAwait(false);
        configuration.StatusMessage = statusMessage;
        return configuration;
    }

    private static async Task<QuickBooksRoutingConfigurationResponse> LoadConfigurationAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var profiles = await context.QuickBooksAllocationProfiles
            .AsNoTracking()
            .Include(item => item.Targets)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rules = await context.QuickBooksRoutingRules
            .AsNoTracking()
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new QuickBooksRoutingConfigurationResponse
        {
            Rules = rules.Select(MapRule).ToList(),
            AllocationProfiles = profiles.Select(MapProfile).ToList(),
            StatusMessage = string.Empty
        };
    }

    private static QuickBooksRoutingRuleDefinition MapRule(QuickBooksRoutingRule rule)
        => new()
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description ?? string.Empty,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            SourceFilePattern = rule.SourceFilePattern ?? string.Empty,
            DefaultEnterprisePattern = rule.DefaultEnterprisePattern ?? string.Empty,
            AccountPattern = rule.AccountPattern ?? string.Empty,
            MemoPattern = rule.MemoPattern ?? string.Empty,
            NamePattern = rule.NamePattern ?? string.Empty,
            SplitAccountPattern = rule.SplitAccountPattern ?? string.Empty,
            TargetEnterprise = rule.TargetEnterprise ?? string.Empty,
            AllocationProfileId = rule.AllocationProfileId
        };

    private static QuickBooksAllocationProfileDefinition MapProfile(QuickBooksAllocationProfile profile)
        => new()
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description ?? string.Empty,
            IsActive = profile.IsActive,
            Targets = profile.Targets
                .OrderBy(item => item.EnterpriseName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new QuickBooksAllocationTargetDefinition
                {
                    Id = item.Id,
                    EnterpriseName = item.EnterpriseName,
                    AllocationPercent = item.AllocationPercent
                })
                .ToList()
        };

    private static IReadOnlyList<QuickBooksImportPreviewRow> ApplyRouting(
        IReadOnlyList<QuickBooksImportPreviewRow> rows,
        string fileName,
        string selectedEnterprise,
        QuickBooksRoutingConfigurationResponse configuration)
    {
        var activeRules = configuration.Rules
            .Where(item => item.IsActive)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var profilesById = configuration.AllocationProfiles.ToDictionary(item => item.Id);
        var defaultEnterprise = ResolveDefaultEnterprise(fileName, selectedEnterprise);
        var routedRows = new List<QuickBooksImportPreviewRow>();

        foreach (var row in rows)
        {
            var matchedRule = activeRules.FirstOrDefault(rule => Matches(rule, row, fileName, defaultEnterprise));
            if (matchedRule is null)
            {
                routedRows.Add(CreateRoutedRow(row, row.Amount, defaultEnterprise, null, null, "No rule matched. Using the selected or inferred enterprise."));
                continue;
            }

            var targetEnterprise = string.IsNullOrWhiteSpace(matchedRule.TargetEnterprise)
                ? defaultEnterprise
                : matchedRule.TargetEnterprise.Trim();

            if (matchedRule.AllocationProfileId is { } allocationProfileId
                && profilesById.TryGetValue(allocationProfileId, out var profile)
                && profile.IsActive)
            {
                routedRows.AddRange(CreateAllocatedRows(row, matchedRule, profile, targetEnterprise));
                continue;
            }

            routedRows.Add(CreateRoutedRow(row, row.Amount, targetEnterprise, matchedRule.Name, null, $"Matched rule '{matchedRule.Name}'."));
        }

        return routedRows;
    }

    private static IEnumerable<QuickBooksImportPreviewRow> CreateAllocatedRows(
        QuickBooksImportPreviewRow row,
        QuickBooksRoutingRuleDefinition rule,
        QuickBooksAllocationProfileDefinition profile,
        string baseEnterprise)
    {
        var activeTargets = profile.Targets
            .Where(item => !string.IsNullOrWhiteSpace(item.EnterpriseName) && item.AllocationPercent > 0m)
            .ToList();

        if (activeTargets.Count == 0 || row.Amount is null)
        {
            yield return CreateRoutedRow(row, row.Amount, baseEnterprise, rule.Name, profile.Name, $"Matched rule '{rule.Name}' but allocation profile '{profile.Name}' has no active targets. Using {baseEnterprise}.");
            yield break;
        }

        var allocations = activeTargets
            .Select(item => (EnterpriseName: item.EnterpriseName.Trim(), Percent: item.AllocationPercent))
            .ToList();

        var allocatedPercent = allocations.Sum(item => item.Percent);
        if (allocatedPercent < 100.00m)
        {
            allocations.Add((baseEnterprise, 100.00m - allocatedPercent));
        }

        var remainingAmount = row.Amount.Value;
        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index];
            var routedAmount = index == allocations.Count - 1
                ? remainingAmount
                : decimal.Round(row.Amount.Value * allocation.Percent / 100.00m, 2, MidpointRounding.AwayFromZero);

            remainingAmount -= routedAmount;

            yield return CreateRoutedRow(
                row,
                routedAmount,
                allocation.EnterpriseName,
                rule.Name,
                BuildAllocationSummary(profile.Name, allocation.EnterpriseName, allocation.Percent),
                $"Matched rule '{rule.Name}' and allocation profile '{profile.Name}'.");
        }
    }

    private static QuickBooksImportPreviewRow CreateRoutedRow(
        QuickBooksImportPreviewRow row,
        decimal? routedAmount,
        string routedEnterprise,
        string? routingRuleName,
        string? allocationSummary,
        string routingReason)
        => row with
        {
            Amount = routedAmount,
            RoutedEnterprise = routedEnterprise,
            RoutingRuleName = routingRuleName,
            AllocationSummary = allocationSummary,
            SourceAmount = row.SourceAmount ?? row.Amount,
            RoutingReason = routingReason
        };

    private static bool Matches(QuickBooksRoutingRuleDefinition rule, QuickBooksImportPreviewRow row, string fileName, string defaultEnterprise)
        => MatchesPattern(rule.SourceFilePattern, fileName)
            && MatchesPattern(rule.DefaultEnterprisePattern, defaultEnterprise)
            && MatchesPattern(rule.AccountPattern, row.AccountName)
            && MatchesPattern(rule.MemoPattern, row.Memo)
            && MatchesPattern(rule.NamePattern, row.Name)
            && MatchesPattern(rule.SplitAccountPattern, row.SplitAccount);

    private static bool MatchesPattern(string? pattern, string? value)
        => string.IsNullOrWhiteSpace(pattern)
            || (!string.IsNullOrWhiteSpace(value) && value.Contains(pattern.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string BuildAllocationSummary(string profileName, string enterpriseName, decimal allocationPercent)
        => $"{profileName}: {enterpriseName} {allocationPercent:0.##}%";

    private static string BuildScopeSummary(IEnumerable<LedgerEntry> ledgerEntries)
    {
        var scopes = ledgerEntries
            .Select(item => item.EntryScope)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return scopes.Count == 0 ? "No scopes recorded" : string.Join(", ", scopes);
    }

    private static string ResolveDefaultEnterprise(string fileName, string? selectedEnterprise)
    {
        if (!string.IsNullOrWhiteSpace(selectedEnterprise) && !GenericScopes.Contains(selectedEnterprise))
        {
            return selectedEnterprise.Trim();
        }

        foreach (var mapping in DefaultEnterpriseMappings)
        {
            if (fileName.Contains(mapping.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.EnterpriseName;
            }
        }

        return "Water Utility";
    }

    private static List<QuickBooksImportPreviewRow> RebuildRawRows(IEnumerable<LedgerEntry> ledgerEntries)
    {
        return ledgerEntries
            .GroupBy(item => item.SourceRowNumber)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var representative = group.OrderBy(item => item.Id).First();
                return new QuickBooksImportPreviewRow(
                    representative.SourceRowNumber,
                    representative.EntryDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                    representative.EntryType,
                    representative.TransactionNumber,
                    representative.Name,
                    representative.Memo,
                    representative.AccountName,
                    representative.SplitAccount,
                    representative.SourceAmount ?? representative.Amount,
                    representative.RunningBalance,
                    representative.ClearedFlag,
                    false);
            })
            .ToList();
    }

    private static string ResolveHistoricalDefaultEnterprise(string fileName, IEnumerable<LedgerEntry> ledgerEntries)
    {
        var originalScope = ledgerEntries.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.OriginalEntryScope) && !GenericScopes.Contains(item.OriginalEntryScope));
        if (originalScope is not null)
        {
            return originalScope.OriginalEntryScope!;
        }

        var currentScope = ledgerEntries.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.EntryScope) && !GenericScopes.Contains(item.EntryScope));
        return currentScope is not null
            ? currentScope.EntryScope
            : ResolveDefaultEnterprise(fileName, null);
    }

    private static string? TryExtractAllocationProfileName(string? allocationSummary)
    {
        if (string.IsNullOrWhiteSpace(allocationSummary))
        {
            return null;
        }

        var separatorIndex = allocationSummary.IndexOf(':');
        return separatorIndex > 0 ? allocationSummary[..separatorIndex].Trim() : allocationSummary.Trim();
    }

    private static decimal? TryExtractAllocationPercent(string? allocationSummary)
    {
        if (string.IsNullOrWhiteSpace(allocationSummary))
        {
            return null;
        }

        var percentToken = allocationSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (percentToken is null)
        {
            return null;
        }

        var trimmed = percentToken.TrimEnd('%');
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}