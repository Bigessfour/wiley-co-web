#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models.Amplify;

[Table("import_batches")]
public class ImportBatch
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("batch_name")]
    public string BatchName { get; set; } = string.Empty;

    [Required]
    [Column("source_system")]
    public string SourceSystem { get; set; } = string.Empty;

    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Required]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("notes")]
    public string? Notes { get; set; }

    public ICollection<SourceFile> SourceFiles { get; set; } = new List<SourceFile>();
}

[Table("source_file_variants")]
public class SourceFileVariant
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("variant_code")]
    public string VariantCode { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    public ICollection<SourceFile> SourceFiles { get; set; } = new List<SourceFile>();
}

[Table("source_files")]
public class SourceFile
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("batch_id")]
    public long BatchId { get; set; }

    public ImportBatch Batch { get; set; } = null!;

    [Column("source_file_variant_id")]
    public long? SourceFileVariantId { get; set; }

    public SourceFileVariant? SourceFileVariant { get; set; }

    [Required]
    [Column("canonical_entity")]
    public string CanonicalEntity { get; set; } = string.Empty;

    [Required]
    [Column("original_file_name")]
    public string OriginalFileName { get; set; } = string.Empty;

    [Column("normalized_file_name")]
    public string? NormalizedFileName { get; set; }

    [Column("sheet_name")]
    public string? SheetName { get; set; }

    [Required]
    [Column("file_hash")]
    public string FileHash { get; set; } = string.Empty;

    [Column("row_count")]
    public int RowCount { get; set; }

    [Column("column_count")]
    public int ColumnCount { get; set; }

    [Column("imported_at")]
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ChartOfAccount> ChartOfAccounts { get; set; } = new List<ChartOfAccount>();
    public ICollection<AmplifyCustomer> Customers { get; set; } = new List<AmplifyCustomer>();
    public ICollection<AmplifyVendor> Vendors { get; set; } = new List<AmplifyVendor>();
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
    public ICollection<TrialBalanceLine> TrialBalanceLines { get; set; } = new List<TrialBalanceLine>();
    public ICollection<ProfitLossMonthlyLine> ProfitLossMonthlyLines { get; set; } = new List<ProfitLossMonthlyLine>();
    public ICollection<BudgetSnapshot> BudgetSnapshots { get; set; } = new List<BudgetSnapshot>();
}

[Table("chart_of_accounts")]
public class ChartOfAccount
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Required]
    [Column("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [Column("account_type")]
    public string? AccountType { get; set; }

    [Column("balance_total", TypeName = "numeric(18,2)")]
    public decimal? BalanceTotal { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("account_number")]
    public string? AccountNumber { get; set; }

    [Column("tax_line")]
    public string? TaxLine { get; set; }
}

[Table("customers")]
public class AmplifyCustomer
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Required]
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [Column("bill_to")]
    public string? BillTo { get; set; }

    [Column("primary_contact")]
    public string? PrimaryContact { get; set; }

    [Column("main_phone")]
    public string? MainPhone { get; set; }

    [Column("fax")]
    public string? Fax { get; set; }

    [Column("balance_total", TypeName = "numeric(18,2)")]
    public decimal? BalanceTotal { get; set; }
}

[Table("vendors")]
public class AmplifyVendor
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Required]
    [Column("vendor_name")]
    public string VendorName { get; set; } = string.Empty;

    [Column("account_number")]
    public string? AccountNumber { get; set; }

    [Column("bill_from")]
    public string? BillFrom { get; set; }

    [Column("primary_contact")]
    public string? PrimaryContact { get; set; }

    [Column("main_phone")]
    public string? MainPhone { get; set; }

    [Column("fax")]
    public string? Fax { get; set; }

    [Column("balance_total", TypeName = "numeric(18,2)")]
    public decimal? BalanceTotal { get; set; }
}

[Table("ledger_entries")]
public class LedgerEntry
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Column("entry_date")]
    public DateOnly? EntryDate { get; set; }

    [Column("entry_type")]
    public string? EntryType { get; set; }

    [Column("transaction_number")]
    public string? TransactionNumber { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("account_name")]
    public string? AccountName { get; set; }

    [Column("split_account")]
    public string? SplitAccount { get; set; }

    [Column("amount", TypeName = "numeric(18,2)")]
    public decimal? Amount { get; set; }

    [Column("running_balance", TypeName = "numeric(18,2)")]
    public decimal? RunningBalance { get; set; }

    [Column("cleared_flag")]
    public string? ClearedFlag { get; set; }

    [Required]
    [Column("entry_scope")]
    public string EntryScope { get; set; } = string.Empty;

    public ICollection<LedgerEntryLine> Lines { get; set; } = new List<LedgerEntryLine>();
}

[Table("ledger_entry_lines")]
public class LedgerEntryLine
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ledger_entry_id")]
    public long LedgerEntryId { get; set; }

    public LedgerEntry LedgerEntry { get; set; } = null!;

    [Column("line_number")]
    public int LineNumber { get; set; }

    [Column("account_name")]
    public string? AccountName { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("split_account")]
    public string? SplitAccount { get; set; }

    [Column("amount", TypeName = "numeric(18,2)")]
    public decimal? Amount { get; set; }

    [Column("running_balance", TypeName = "numeric(18,2)")]
    public decimal? RunningBalance { get; set; }

    [Column("is_split_row")]
    public bool IsSplitRow { get; set; }
}

[Table("trial_balance_lines")]
public class TrialBalanceLine
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Column("as_of_date")]
    public DateOnly AsOfDate { get; set; }

    [Required]
    [Column("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [Column("debit", TypeName = "numeric(18,2)")]
    public decimal? Debit { get; set; }

    [Column("credit", TypeName = "numeric(18,2)")]
    public decimal? Credit { get; set; }

    [Column("balance", TypeName = "numeric(18,2)")]
    public decimal? Balance { get; set; }
}

[Table("profit_loss_monthly_lines")]
public class ProfitLossMonthlyLine
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long SourceFileId { get; set; }

    public SourceFile SourceFile { get; set; } = null!;

    [Column("source_row_number")]
    public int SourceRowNumber { get; set; }

    [Required]
    [Column("line_label")]
    public string LineLabel { get; set; } = string.Empty;

    [Column("line_type")]
    public string? LineType { get; set; }

    [Column("jan_amount", TypeName = "numeric(18,2)")]
    public decimal? JanAmount { get; set; }

    [Column("feb_amount", TypeName = "numeric(18,2)")]
    public decimal? FebAmount { get; set; }

    [Column("mar_amount", TypeName = "numeric(18,2)")]
    public decimal? MarAmount { get; set; }

    [Column("apr_amount", TypeName = "numeric(18,2)")]
    public decimal? AprAmount { get; set; }

    [Column("may_amount", TypeName = "numeric(18,2)")]
    public decimal? MayAmount { get; set; }

    [Column("jun_amount", TypeName = "numeric(18,2)")]
    public decimal? JunAmount { get; set; }

    [Column("jul_amount", TypeName = "numeric(18,2)")]
    public decimal? JulAmount { get; set; }

    [Column("aug_amount", TypeName = "numeric(18,2)")]
    public decimal? AugAmount { get; set; }

    [Column("sep_amount", TypeName = "numeric(18,2)")]
    public decimal? SepAmount { get; set; }

    [Column("oct_amount", TypeName = "numeric(18,2)")]
    public decimal? OctAmount { get; set; }

    [Column("nov_amount", TypeName = "numeric(18,2)")]
    public decimal? NovAmount { get; set; }

    [Column("dec_amount", TypeName = "numeric(18,2)")]
    public decimal? DecAmount { get; set; }

    [Column("total_amount", TypeName = "numeric(18,2)")]
    public decimal? TotalAmount { get; set; }
}

[Table("budget_snapshots")]
public class BudgetSnapshot
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("source_file_id")]
    public long? SourceFileId { get; set; }

    public SourceFile? SourceFile { get; set; }

    [Required]
    [Column("snapshot_name")]
    public string SnapshotName { get; set; } = string.Empty;

    [Column("snapshot_date")]
    public DateOnly? SnapshotDate { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("payload", TypeName = "jsonb")]
    public string? Payload { get; set; }
}