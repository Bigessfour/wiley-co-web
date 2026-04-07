using WileyCoWeb.Contracts;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class QuickBooksImportAssistantService
{
	private const string CanonicalImportRules = "QuickBooks Desktop exports are canonical actuals. Reject duplicate files by file hash, keep enterprise and fiscal year aligned to the selected workspace context, and prefer preview-first troubleshooting before commit.";
	private const string SystemPrompt = "You are a QuickBooks Desktop import specialist for municipal clerks. Answer only the user's question, using the provided import context. Be practical, concise, and specific about file format, duplicate prevention, enterprise/fiscal year selection, row mapping, date parsing, and commit troubleshooting. Do not invent facts beyond the context.";

	private readonly ILogger<QuickBooksImportAssistantService> logger;
	private readonly IConfiguration configuration;
	private readonly Lazy<IChatCompletionService?> chatService;
	private readonly IGrokApiKeyProvider? apiKeyProvider;

	public QuickBooksImportAssistantService(
		IConfiguration configuration,
		ILogger<QuickBooksImportAssistantService> logger,
		IGrokApiKeyProvider? apiKeyProvider = null)
	{
		this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.apiKeyProvider = apiKeyProvider;
		chatService = new Lazy<IChatCompletionService?>(InitializeChatService);
	}

	public async Task<QuickBooksImportGuidanceResponse> AskAsync(QuickBooksImportGuidanceRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var contextSummary = BuildContextSummary(request.Preview);
		if (request.Preview is null)
		{
			return new QuickBooksImportGuidanceResponse(
				request.Question,
				"Load a QuickBooks export first so I can inspect the rows, duplicate status, and selected workspace context.",
				true,
				contextSummary);
		}

		var question = string.IsNullOrWhiteSpace(request.Question)
			? "What should I know about this QuickBooks import?"
			: request.Question.Trim();

		var assistant = chatService.Value;
		if (assistant is not null)
		{
			try
			{
				var chatHistory = new ChatHistory();
				chatHistory.AddSystemMessage(SystemPrompt);
				chatHistory.AddUserMessage(BuildUserPrompt(question, request.Preview));

				var response = await assistant.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);
				var answer = response.Content?.Trim();
				if (!string.IsNullOrWhiteSpace(answer))
				{
					return new QuickBooksImportGuidanceResponse(question, answer, false, contextSummary);
				}
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "QuickBooks import guidance request fell back to deterministic rules");
			}
		}

		return new QuickBooksImportGuidanceResponse(question, BuildFallbackAnswer(question, request.Preview), true, contextSummary);
	}

	private IChatCompletionService? InitializeChatService()
	{
		try
		{
			var apiKey = apiKeyProvider?.ApiKey
				?? configuration["XAI:ApiKey"]
				?? configuration["xAI:ApiKey"]
				?? configuration["XAI_API_KEY"];
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				return null;
			}

			var model = configuration["Grok:Model"] ?? configuration["XAI:Model"] ?? "grok-4.1";

			var kernelBuilder = Kernel.CreateBuilder();
			kernelBuilder.AddOpenAIChatCompletion(
				modelId: model,
				apiKey: apiKey,
				endpoint: new Uri("https://api.x.ai/v1"));

			var kernel = kernelBuilder.Build();
			logger.LogInformation(
				"QuickBooks import assistant initialized with Semantic Kernel (model: {Model}, apiKeySource: {ApiKeySource})",
				model,
				apiKeyProvider?.GetConfigurationSource() ?? "configuration");

			return kernel.GetRequiredService<IChatCompletionService>();
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "QuickBooks import assistant could not initialize Semantic Kernel");
			return null;
		}
	}

	private static string BuildUserPrompt(string question, QuickBooksImportPreviewResponse preview)
	{
		var builder = new StringBuilder();
		builder.AppendLine("Import context:");
		builder.AppendLine($"File: {preview.FileName}");
		builder.AppendLine($"Enterprise: {preview.SelectedEnterprise}");
		builder.AppendLine($"Fiscal year: {preview.SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)}");
		builder.AppendLine($"Rows parsed: {preview.TotalRows.ToString(CultureInfo.InvariantCulture)}");
		builder.AppendLine($"Duplicate rows: {preview.DuplicateRows.ToString(CultureInfo.InvariantCulture)}");
		builder.AppendLine($"Duplicate file: {preview.IsDuplicate}");
		builder.AppendLine($"Status: {preview.StatusMessage}");
		builder.AppendLine();
		builder.AppendLine(CanonicalImportRules);
		builder.AppendLine();
		builder.AppendLine("Sample rows:");

		foreach (var row in preview.Rows.Take(5))
		{
			builder.AppendLine($"- Row {row.RowNumber.ToString(CultureInfo.InvariantCulture)} | {row.EntryDate} | {row.EntryType} | {row.Name} | {row.AccountName} | {row.Amount?.ToString("C2", CultureInfo.InvariantCulture) ?? "n/a"} | Duplicate: {row.IsDuplicate}");
		}

		builder.AppendLine();
		builder.AppendLine($"Question: {question}");
		builder.AppendLine("Answer in plain language that helps a clerk resolve the problem or proceed safely.");

		return builder.ToString();
	}

	private static string BuildContextSummary(QuickBooksImportPreviewResponse? preview)
	{
		if (preview is null)
		{
			return "No QuickBooks preview has been loaded yet.";
		}

		var sampleRows = preview.Rows.Take(3).Select(row => $"Row {row.RowNumber}: {row.EntryDate ?? "n/a"} {row.EntryType ?? "n/a"} {row.Name ?? "n/a"} {row.Amount?.ToString("C2", CultureInfo.InvariantCulture) ?? "n/a"}");
		return $"{preview.FileName} for {preview.SelectedEnterprise} FY {preview.SelectedFiscalYear} with {preview.TotalRows} rows. Duplicate file: {preview.IsDuplicate}. Preview status: {preview.StatusMessage}. Sample: {string.Join("; ", sampleRows)}";
	}

	private static string BuildFallbackAnswer(string question, QuickBooksImportPreviewResponse preview)
	{
		var loweredQuestion = question.ToLowerInvariant();

		if (preview.IsDuplicate || loweredQuestion.Contains("duplicate") || loweredQuestion.Contains("already imported"))
		{
			return "This file is treated as a duplicate because the file hash already exists in the import history. Use a different export, or refresh the source system and re-export only if the file contents truly changed.";
		}

		if (loweredQuestion.Contains("date") || loweredQuestion.Contains("format"))
		{
			return "Check that the export uses recognizable Date, Transaction Type, Num, Name, Memo, Account, Split, Amount, Balance, and Clr columns. Date parsing expects common QuickBooks date formats, and malformed rows usually need to be corrected in the source export before commit.";
		}

		if (loweredQuestion.Contains("enterprise") || loweredQuestion.Contains("fiscal"))
		{
			return "Make sure the selected enterprise and fiscal year match the export being loaded. The clerk should only commit when the file is attached to the intended workspace context.";
		}

		if (loweredQuestion.Contains("row") || loweredQuestion.Contains("column") || loweredQuestion.Contains("mapping"))
		{
			return "The preview maps QuickBooks columns into ledger rows. If something looks wrong, verify the export headers and compare the preview sample rows against the original CSV or XLSX file.";
		}

		return "Review the preview rows, confirm the selected enterprise and fiscal year, and only commit after the file-level duplicate check passes. If you want, ask a more specific question about duplicate blocking, missing columns, dates, or row mapping.";
	}
}
