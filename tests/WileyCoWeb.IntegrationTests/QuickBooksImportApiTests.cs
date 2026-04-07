using System.Net.Http.Headers;
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
		Assert.Contains("Imported 2 QuickBooks rows", firstCommit.StatusMessage);

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
		using var form = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

		form.Add(fileContent, "file", "quickbooks-ledger.csv");
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
}