namespace WileyCoWeb.Contracts;

public sealed record QuickBooksImportPreviewRow(
	int RowNumber,
	string? EntryDate,
	string? EntryType,
	string? TransactionNumber,
	string? Name,
	string? Memo,
	string? AccountName,
	string? SplitAccount,
	decimal? Amount,
	decimal? RunningBalance,
	string? ClearedFlag,
	bool IsDuplicate,
	string? RoutedEnterprise = null,
	string? RoutingRuleName = null,
	string? AllocationSummary = null,
	decimal? SourceAmount = null,
	string? RoutingReason = null);

public sealed record QuickBooksImportPreviewResponse(
	string FileName,
	string FileHash,
	string SelectedEnterprise,
	int SelectedFiscalYear,
	int TotalRows,
	int DuplicateRows,
	bool IsDuplicate,
	string StatusMessage,
	IReadOnlyList<QuickBooksImportPreviewRow> Rows);

public sealed record QuickBooksImportCommitResponse(
	string FileName,
	string FileHash,
	string SelectedEnterprise,
	int SelectedFiscalYear,
	int ImportedRows,
	long BatchId,
	bool IsDuplicate,
	string StatusMessage,
	IReadOnlyList<string> Warnings);

public sealed record QuickBooksImportGuidanceRequest(
	string Question,
	QuickBooksImportPreviewResponse? Preview);

public sealed record QuickBooksImportGuidanceResponse(
	string Question,
	string Answer,
	bool UsedFallback,
	string ContextSummary);