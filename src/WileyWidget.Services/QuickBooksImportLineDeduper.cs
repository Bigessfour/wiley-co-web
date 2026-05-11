using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

internal static class QuickBooksImportLineDeduper
{
	internal static List<QuickBooksImportPreviewRow> RemoveExactDuplicateLines(IReadOnlyList<QuickBooksImportPreviewRow> rows)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var result = new List<QuickBooksImportPreviewRow>(rows.Count);
		foreach (var row in rows)
		{
			if (!seen.Add(QuickBooksLedgerSignature.FromPreviewRow(row)))
			{
				continue;
			}

			result.Add(row);
		}

		return result;
	}
}
