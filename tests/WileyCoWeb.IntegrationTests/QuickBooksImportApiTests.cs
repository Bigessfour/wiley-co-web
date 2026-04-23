using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using WileyCoWeb.Contracts;
using WileyCoWeb.IntegrationTests.Infrastructure;

namespace WileyCoWeb.IntegrationTests;

public sealed class QuickBooksImportApiTests : IClassFixture<ApiApplicationFactory>
{
	private readonly ApiApplicationFactory factory;
	private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

	public QuickBooksImportApiTests(ApiApplicationFactory factory)
	{
		this.factory = factory;
	}

	[Fact]
	public async Task Preview_ReturnsParsedQuickBooksRows()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		var response = await PostImportAsync(client, "/api/imports/quickbooks/preview", CreateQuickBooksCsv());

		response.EnsureSuccessStatusCode();
		var payload = await response.Content.ReadFromJsonAsync<QuickBooksImportPreviewResponse>(jsonOptions);

		Assert.NotNull(payload);
		Assert.Equal(2, payload.TotalRows);
		Assert.False(payload.IsDuplicate);
		Assert.Equal("Water Utility", payload.SelectedEnterprise);
		Assert.Equal(2026, payload.SelectedFiscalYear);
		Assert.Equal(2, payload.Rows.Count);
		Assert.Equal("Water Billing", payload.Rows[0].Memo);
	}

	[Fact]
	public async Task Preview_ReturnsParsedQuickBooksRows_ForExcelWorkbookWithTipsSheet()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		var response = await PostImportAsync(
			client,
			"/api/imports/quickbooks/preview",
			CreateQuickBooksWorkbookWithTipsSheet(),
			"quickbooks-ledger.xlsx",
			"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

		response.EnsureSuccessStatusCode();
		var payload = await response.Content.ReadFromJsonAsync<QuickBooksImportPreviewResponse>(jsonOptions);

		Assert.NotNull(payload);
		Assert.Equal(1, payload.TotalRows);
		Assert.False(payload.IsDuplicate);
		Assert.Equal("2026-01-02", payload.Rows[0].EntryDate);
		Assert.Equal("Deposit", payload.Rows[0].EntryType);
		Assert.Equal("WATER PAYMENTS", payload.Rows[0].Name);
	}

	[Fact]
	public async Task Commit_PersistsRows_AndRejectsDuplicateFile()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		var firstCommitResponse = await PostImportAsync(client, "/api/imports/quickbooks/commit", CreateQuickBooksCsv());
		firstCommitResponse.EnsureSuccessStatusCode();

		var firstCommit = await firstCommitResponse.Content.ReadFromJsonAsync<QuickBooksImportCommitResponse>(jsonOptions);
		Assert.NotNull(firstCommit);
		Assert.False(firstCommit.IsDuplicate);
		Assert.Equal(2, firstCommit.ImportedRows);
		Assert.Contains("Imported 2 QuickBooks routed row", firstCommit.StatusMessage);
		Assert.Contains("Water Utility", firstCommit.StatusMessage);

		var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
		await using var context = await contextFactory.CreateDbContextAsync();
		Assert.Equal(1, await context.ImportBatches.CountAsync());
		Assert.Equal(1, await context.SourceFiles.CountAsync());
		Assert.Equal(2, await context.LedgerEntries.CountAsync());

		var duplicateResponse = await PostImportAsync(client, "/api/imports/quickbooks/commit", CreateQuickBooksCsv());
		Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

		var duplicateCommit = await duplicateResponse.Content.ReadFromJsonAsync<QuickBooksImportCommitResponse>(jsonOptions);
		Assert.NotNull(duplicateCommit);
		Assert.True(duplicateCommit.IsDuplicate);
		Assert.Equal(0, duplicateCommit.ImportedRows);
		Assert.Contains("Duplicate QuickBooks import blocked", duplicateCommit.StatusMessage);
	}

	[Fact]
	public async Task Assistant_ReturnsContextualGuidanceForLoadedPreview()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		var previewResponse = await PostImportAsync(client, "/api/imports/quickbooks/preview", CreateQuickBooksCsv());
		previewResponse.EnsureSuccessStatusCode();

		var preview = await previewResponse.Content.ReadFromJsonAsync<QuickBooksImportPreviewResponse>(jsonOptions);
		Assert.NotNull(preview);

		var assistantResponse = await client.PostAsJsonAsync("/api/imports/quickbooks/assistant", new QuickBooksImportGuidanceRequest("Why would this file be blocked as a duplicate?", preview));

		assistantResponse.EnsureSuccessStatusCode();

		var guidance = await assistantResponse.Content.ReadFromJsonAsync<QuickBooksImportGuidanceResponse>(jsonOptions);
		Assert.NotNull(guidance);
		Assert.Equal("Why would this file be blocked as a duplicate?", guidance.Question);
		Assert.Contains("quickbooks-ledger.csv", guidance.ContextSummary);
		Assert.False(string.IsNullOrWhiteSpace(guidance.Answer));
	}

	[Fact]
	public async Task Preview_ReturnsBadRequest_WhenRequiredFormFieldsAreMissing()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		using var form = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateQuickBooksCsv()));
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

		form.Add(fileContent, "file", "quickbooks-ledger.csv");
		form.Add(new StringContent("2026"), "selectedFiscalYear");

		var response = await client.PostAsync("/api/imports/quickbooks/preview", form);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Assistant_ReturnsBadRequest_WhenQuestionIsMissing()
	{
		await factory.ResetDatabaseAsync();
		using var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/imports/quickbooks/assistant",
			new QuickBooksImportGuidanceRequest(string.Empty, null));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	private static async Task<HttpResponseMessage> PostImportAsync(HttpClient client, string requestUri, string csvContent)
	{
		return await PostImportAsync(client, requestUri, Encoding.UTF8.GetBytes(csvContent), "quickbooks-ledger.csv", "text/csv");
	}

	private static async Task<HttpResponseMessage> PostImportAsync(HttpClient client, string requestUri, byte[] fileBytes, string fileName, string contentType)
	{
		using var form = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(fileBytes);
		fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

		form.Add(fileContent, "file", fileName);
		form.Add(new StringContent("Water Utility"), "selectedEnterprise");
		form.Add(new StringContent("2026"), "selectedFiscalYear");

		return await client.PostAsync(requestUri, form);
	}

	private static string CreateQuickBooksCsv()
	{
		return "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr\n" +
			   "01/01/2026,Invoice,1001,Town of Wiley,Water Billing,Water Revenue,Accounts Receivable,125.00,125.00,C\n" +
			   "01/02/2026,Payment,1002,Town of Wiley,Payment Received,Accounts Receivable,Water Revenue,-125.00,0.00,C\n";
	}

	private static byte[] CreateQuickBooksWorkbookWithTipsSheet()
	{
		using var stream = new MemoryStream();
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			WriteEntry(
				archive,
				"[Content_Types].xml",
				"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+ "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
				+ "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
				+ "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
				+ "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
				+ "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
				+ "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
				+ "</Types>");

			WriteEntry(
				archive,
				"_rels/.rels",
				"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+ "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
				+ "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
				+ "</Relationships>");

			WriteEntry(
				archive,
				"xl/workbook.xml",
				"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+ "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
				+ "<sheets>"
				+ "<sheet name=\"QuickBooks Desktop Export Tips\" sheetId=\"1\" r:id=\"rId1\"/>"
				+ "<sheet name=\"Sheet1\" sheetId=\"2\" r:id=\"rId2\"/>"
				+ "</sheets>"
				+ "</workbook>");

			WriteEntry(
				archive,
				"xl/_rels/workbook.xml.rels",
				"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+ "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
				+ "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
				+ "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>"
				+ "</Relationships>");

			WriteEntry(
				archive,
				"xl/worksheets/sheet1.xml",
				BuildWorksheetXml([
					["QuickBooks Desktop Export Tips"],
					["Do not edit the exported report layout before upload."]
				]));

			WriteEntry(
				archive,
				"xl/worksheets/sheet2.xml",
				BuildWorksheetXml([
					["", "", "", "Type", "", "Date", "", "Num", "", "Name", "", "Memo", "", "Account", "", "Clr", "", "Split", "", "Amount", "", "Balance"],
					["Jan - Dec 26"],
					["", "", "", "Deposit", "", "46024", "", "", "", "WATER PAYMENTS", "", "VIA CREDIT CARD", "", "105 · ACCOUNTS RECEIVABLE", "", "", "", "101 · CASH IN BANK - UTILITY", "", "-362.90", "", "0.00"]
				]));
		}

		return stream.ToArray();
	}

	private static string BuildWorksheetXml(IEnumerable<string[]> rows)
	{
		var builder = new StringBuilder();
		builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
		builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

		var rowNumber = 1;
		foreach (var row in rows)
		{
			builder.Append($"<row r=\"{rowNumber}\">");
			for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
			{
				if (string.IsNullOrEmpty(row[columnIndex]))
				{
					continue;
				}

				var cellReference = $"{ToColumnName(columnIndex + 1)}{rowNumber}";
				builder.Append($"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(row[columnIndex])}</t></is></c>");
			}

			builder.Append("</row>");
			rowNumber++;
		}

		builder.Append("</sheetData></worksheet>");
		return builder.ToString();
	}

	private static string ToColumnName(int columnNumber)
	{
		var dividend = columnNumber;
		var columnName = string.Empty;

		while (dividend > 0)
		{
			var modulo = (dividend - 1) % 26;
			columnName = Convert.ToChar('A' + modulo) + columnName;
			dividend = (dividend - modulo) / 26;
		}

		return columnName;
	}

	private static void WriteEntry(ZipArchive archive, string entryName, string content)
	{
		var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
		using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		writer.Write(content);
	}
}