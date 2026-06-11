using System.Globalization;
using WileyCoWeb.Contracts;
using WileyWidget.Models.ImportSchema;

namespace WileyWidget.Services;

internal static class QuickBooksLedgerSignature
{
	internal static string NormalizeSignatureText(string? value)
		=> string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

	internal static string NormalizeSignatureDate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
			? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
			: NormalizeSignatureText(value);
	}

	internal static string FromPreviewRow(QuickBooksImportPreviewRow row)
		=> string.Join("|",
			NormalizeSignatureText(row.RoutedEnterprise),
			NormalizeSignatureDate(row.EntryDate),
			NormalizeSignatureText(row.EntryType),
			NormalizeSignatureText(row.TransactionNumber),
			NormalizeSignatureText(row.Name),
			NormalizeSignatureText(row.Memo),
			NormalizeSignatureText(row.AccountName),
			NormalizeSignatureText(row.SplitAccount),
			row.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
			NormalizeSignatureText(row.ClearedFlag));

	internal static string FromLedgerEntry(LedgerEntry entry)
		=> string.Join("|",
			NormalizeSignatureText(entry.EntryScope),
			entry.EntryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
			NormalizeSignatureText(entry.EntryType),
			NormalizeSignatureText(entry.TransactionNumber),
			NormalizeSignatureText(entry.Name),
			NormalizeSignatureText(entry.Memo),
			NormalizeSignatureText(entry.AccountName),
			NormalizeSignatureText(entry.SplitAccount),
			entry.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
			NormalizeSignatureText(entry.ClearedFlag));
}
