namespace WileyCoWeb.Contracts;

public sealed class QuickBooksRoutingRuleDefinition
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    public string SourceFilePattern { get; set; } = string.Empty;

    public string DefaultEnterprisePattern { get; set; } = string.Empty;

    public string AccountPattern { get; set; } = string.Empty;

    public string MemoPattern { get; set; } = string.Empty;

    public string NamePattern { get; set; } = string.Empty;

    public string SplitAccountPattern { get; set; } = string.Empty;

    public string TargetEnterprise { get; set; } = string.Empty;

    public long? AllocationProfileId { get; set; }
}

public sealed class QuickBooksAllocationTargetDefinition
{
    public long Id { get; set; }

    public string EnterpriseName { get; set; } = string.Empty;

    public decimal AllocationPercent { get; set; }
}

public sealed class QuickBooksAllocationProfileDefinition
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<QuickBooksAllocationTargetDefinition> Targets { get; set; } = [];
}

public sealed class QuickBooksRoutingConfigurationRequest
{
    public List<QuickBooksRoutingRuleDefinition> Rules { get; set; } = [];

    public List<QuickBooksAllocationProfileDefinition> AllocationProfiles { get; set; } = [];
}

public sealed class QuickBooksRoutingConfigurationResponse
{
    public List<QuickBooksRoutingRuleDefinition> Rules { get; set; } = [];

    public List<QuickBooksAllocationProfileDefinition> AllocationProfiles { get; set; } = [];

    public string StatusMessage { get; set; } = string.Empty;
}

public sealed class QuickBooksImportHistoryItem
{
    public long SourceFileId { get; set; }

    public long BatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ScopeSummary { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public string ImportedAtUtc { get; set; } = string.Empty;
}

public sealed class QuickBooksImportHistoryResponse
{
    public List<QuickBooksImportHistoryItem> Items { get; set; } = [];

    public string StatusMessage { get; set; } = string.Empty;
}

public sealed class QuickBooksHistoricalRerouteRequest
{
    public long SourceFileId { get; set; }
}

public sealed class QuickBooksHistoricalRerouteResponse
{
    public long SourceFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int SourceRowCount { get; set; }

    public int RoutedRowCount { get; set; }

    public string StatusMessage { get; set; } = string.Empty;
}