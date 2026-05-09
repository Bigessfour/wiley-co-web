using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Plugins;

namespace WileyWidget.Services;

public sealed class WorkspaceAiKernelProvider : IWorkspaceAiKernelProvider
{
    private readonly IConfiguration configuration;
    private readonly ILogger<WorkspaceAiKernelProvider> logger;
    private readonly IUserContext userContext;
    private readonly IConversationRepository conversationRepository;
    private readonly IWileyWidgetContextService contextService;
    private readonly IWorkspaceKnowledgeService workspaceKnowledgeService;
    private readonly IHttpClientFactory? httpClientFactory;
    private readonly IGrokApiKeyProvider? apiKeyProvider;
    private readonly Lazy<WorkspaceAiKernelInitializationResult> initializationResult;

    public WorkspaceAiKernelProvider(
        IConfiguration configuration,
        ILogger<WorkspaceAiKernelProvider> logger,
        IUserContext userContext,
        IConversationRepository conversationRepository,
        IWileyWidgetContextService contextService,
        IWorkspaceKnowledgeService workspaceKnowledgeService,
        IHttpClientFactory? httpClientFactory = null,
        IGrokApiKeyProvider? apiKeyProvider = null)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        this.conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        this.contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        this.workspaceKnowledgeService = workspaceKnowledgeService ?? throw new ArgumentNullException(nameof(workspaceKnowledgeService));
        this.httpClientFactory = httpClientFactory;
        this.apiKeyProvider = apiKeyProvider;
        initializationResult = new Lazy<WorkspaceAiKernelInitializationResult>(Initialize);
    }

    public WorkspaceAiServiceConfiguration GetConfiguration()
        => WorkspaceAiKernelFactory.ResolveConfiguration(configuration, apiKeyProvider);

    public WorkspaceAiKernelInitializationResult GetInitializationResult()
        => initializationResult.Value;

    private WorkspaceAiKernelInitializationResult Initialize()
    {
        var serviceConfiguration = GetConfiguration();
        var apiKeyResolution = serviceConfiguration.ApiKeyResolution;

        if (!serviceConfiguration.Enabled)
        {
            return new WorkspaceAiKernelInitializationResult(
                Context: null,
                IsAvailable: false,
                IsApiKeyVisibleToProcess: !string.IsNullOrWhiteSpace(apiKeyResolution.ApiKey),
                ApiKeySource: apiKeyResolution.ApiKeySource,
                StatusCode: "disabled",
                StatusMessage: "Semantic Kernel is disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(apiKeyResolution.ApiKey))
        {
            logger.LogError(
                "Workspace AI kernel provider could not find a usable API key (ApiKeySource={ApiKeySource}, EnvironmentKeyPresent={EnvironmentKeyPresent}, ConfigDirectKeyPresent={ConfigDirectKeyPresent}, ConfigNamedKeyPresent={ConfigNamedKeyPresent}, SecretName={SecretName}, ChatEndpoint={ChatEndpoint}, LegacyEndpoint={LegacyEndpoint}).",
                apiKeyResolution.ApiKeySource,
                apiKeyResolution.EnvironmentPresent,
                apiKeyResolution.ConfigDirectPresent,
                apiKeyResolution.ConfigNamedPresent,
                apiKeyResolution.SecretName,
                serviceConfiguration.ChatCompletionEndpoint,
                serviceConfiguration.LegacyResponsesEndpoint);

            return new WorkspaceAiKernelInitializationResult(
                Context: null,
                IsAvailable: false,
                IsApiKeyVisibleToProcess: false,
                ApiKeySource: apiKeyResolution.ApiKeySource,
                StatusCode: "missing_api_key",
                StatusMessage: "No usable XAI_API_KEY value was visible to the process.");
        }

        try
        {
            var context = WorkspaceAiKernelFactory.CreateKernelContext(
                serviceConfiguration,
                WorkspaceAiKernelFactory.DefaultSemanticKernelModel,
                httpClientFactory,
                ConfigureBuilder);

            logger.LogInformation(
                "Workspace AI kernel provider initialized Semantic Kernel (model: {Model}, apiKeySource: {ApiKeySource}, chatEndpoint: {ChatEndpoint}, legacyEndpoint: {LegacyEndpoint}).",
                serviceConfiguration.ResolveModelOrDefault(WorkspaceAiKernelFactory.DefaultSemanticKernelModel),
                apiKeyResolution.ApiKeySource,
                serviceConfiguration.ChatCompletionEndpoint,
                serviceConfiguration.LegacyResponsesEndpoint);

            return new WorkspaceAiKernelInitializationResult(
                context,
                IsAvailable: true,
                IsApiKeyVisibleToProcess: true,
                ApiKeySource: apiKeyResolution.ApiKeySource,
                StatusCode: "available",
                StatusMessage: "Semantic Kernel initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Workspace AI kernel provider failed to initialize Semantic Kernel (ApiKeySource={ApiKeySource}, ChatEndpoint={ChatEndpoint}, LegacyEndpoint={LegacyEndpoint}).",
                apiKeyResolution.ApiKeySource,
                serviceConfiguration.ChatCompletionEndpoint,
                serviceConfiguration.LegacyResponsesEndpoint);

            return new WorkspaceAiKernelInitializationResult(
                Context: null,
                IsAvailable: false,
                IsApiKeyVisibleToProcess: true,
                ApiKeySource: apiKeyResolution.ApiKeySource,
                StatusCode: "kernel_initialization_failed",
                StatusMessage: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ConfigureBuilder(IKernelBuilder kernelBuilder)
    {
        kernelBuilder.Plugins.AddFromType<Plugins.System.TimePlugin>();
        kernelBuilder.Plugins.AddFromType<Plugins.Development.CodebaseInsightPlugin>();
        kernelBuilder.Plugins.AddFromType<AnomalyDetectionPlugin>();

        var userContextPlugin = new UserContextPlugin(userContext, conversationRepository, contextService, workspaceKnowledgeService);
        kernelBuilder.Plugins.AddFromObject(userContextPlugin, "JarvisUserContext");
    }
}