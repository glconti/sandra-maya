using Microsoft.Extensions.Options;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Configuration;
using SandraMaya.Capabilities.Persistence;
using SandraMaya.Capabilities.Services;
using SandraMaya.Infrastructure;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Assistant.ToolCalling;
using SandraMaya.Host.Assistant.ToolCalling.Tools;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Health;
using SandraMaya.Host.Jobs;
using SandraMaya.Host.Mcp;
using SandraMaya.Host.Playwright;
using SandraMaya.Host.Services;
using SandraMaya.Host.Storage;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSandraMayaHost(this IServiceCollection services, IConfiguration configuration)
    {
        var telegramOptions = configuration.GetSection(TelegramOptions.SectionName).Get<TelegramOptions>() ?? new TelegramOptions();

        services
            .AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AzureOpenAiOptions>()
            .Bind(configuration.GetSection(AzureOpenAiOptions.SectionName))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProviderType) ||
                           string.Equals(options.ProviderType, "azure", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(options.ProviderType, "openai", StringComparison.OrdinalIgnoreCase),
                "AzureOpenAi:ProviderType must be either 'azure' or 'openai'.");

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Root), "Storage:Root is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CapabilityRegistryFileName), "Storage:CapabilityRegistryFileName is required.")
            .ValidateOnStart();

        services
            .AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DotNetCommand), "Runtime:DotNetCommand is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.NodeCommand), "Runtime:NodeCommand is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PlaywrightCommand), "Runtime:PlaywrightCommand is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PythonCommand), "Runtime:PythonCommand is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PowerShellCommand), "Runtime:PowerShellCommand is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BashCommand), "Runtime:BashCommand is required.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var environment = sp.GetRequiredService<IHostEnvironment>();
            return StorageLayout.Create(options, environment.ContentRootPath);
        });

        services.AddMemoryFoundation(
            sp => sp.GetRequiredService<StorageLayout>().SqlitePath,
            sp => sp.GetRequiredService<StorageLayout>().UploadsPath);

        // Override the placeholder cover letter service with the real AI-powered one
        services.AddScoped<ICoverLetterDraftService, AzureOpenAiCoverLetterDraftService>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RuntimeOptions>>().Value;
            return new CapabilityRuntimeCommandOptions
            {
                DotNetCommand = options.DotNetCommand,
                NodeCommand = options.NodeCommand,
                PlaywrightCommand = options.PlaywrightCommand,
                PythonCommand = options.PythonCommand,
                PowerShellCommand = options.PowerShellCommand,
                BashCommand = options.BashCommand
            };
        });

        services.AddSingleton<ICapabilityStore>(sp =>
        {
            var storage = sp.GetRequiredService<StorageLayout>();
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new FileCapabilityStore(new CapabilityStoreOptions(storage.CapabilitiesPath, options.CapabilityRegistryFileName));
        });
        services.AddSingleton<ICapabilityRegistryService, CapabilityRegistryService>();
        services.AddSingleton<ICapabilityActivityService, CapabilityActivityService>();
        services.AddSingleton<ICapabilityExecutionPlanResolver, CapabilityExecutionPlanResolver>();

        // MCP client manager (stub — config management only)
        services.AddSingleton<McpClientManager>();

        // User resolution
        services.AddScoped<IUserResolutionService, UserResolutionService>();

        // Playwright execution service
        services.AddSingleton<IPlaywrightExecutionService, PlaywrightExecutionService>();

        // Memory & CV tools
        services.AddScoped<IToolHandler, MemorySaveNoteTool>();
        services.AddScoped<IToolHandler, MemorySearchTool>();
        services.AddScoped<IToolHandler, MemoryGetCvTool>();
        services.AddScoped<IToolHandler, CvIngestTool>();

        // Job tools
        services.AddSingleton<IToolHandler, JobListSitesTool>();
        services.AddScoped<IToolHandler, JobSearchSavedTool>();
        services.AddScoped<IToolHandler, JobCrawlTool>();
        services.AddScoped<IToolHandler, JobTrackApplicationTool>();
        services.AddScoped<IToolHandler, JobListApplicationsTool>();
        services.AddScoped<IToolHandler, JobActivitySummaryTool>();

        // Cover letter tool
        services.AddScoped<IToolHandler, CoverLetterDraftTool>();

        // Web interaction tools (Playwright-based)
        services.AddSingleton<IToolHandler, WebBrowseTool>();
        services.AddSingleton<IToolHandler, WebSearchTool>();
        services.AddSingleton<IToolHandler, WebExtractStructuredTool>();
        services.AddSingleton<IToolHandler, WebScreenshotTool>();

        // Capability tools
        services.AddScoped<IToolHandler, CapabilityListTool>();
        services.AddScoped<IToolHandler, CapabilityProposeTool>();
        services.AddScoped<IToolHandler, CapabilityExecuteTool>();

        // MCP management tools
        services.AddSingleton<IToolHandler, McpListServersTool>();
        services.AddSingleton<IToolHandler, McpAddServerTool>();
        services.AddSingleton<IToolHandler, McpRemoveServerTool>();

        services.AddScoped<ToolRegistry>();
        services.AddScoped<SystemPromptBuilder>();

        services.AddSingleton<ConversationHistoryStore>();
        services.AddSingleton<IAssistantSessionStore, InMemoryAssistantSessionStore>();
        services.AddScoped<IAssistantOrchestrator, AzureOpenAiAssistantOrchestrator>();
        services.AddHostedService<StorageBootstrapService>();

        if (!string.IsNullOrWhiteSpace(telegramOptions.BotToken))
        {
            services.AddSingleton<IInboundMessageRouter, AssistantMessageRouter>();
            services.AddSingleton<IOutboundMessageDispatcher, TelegramOutboundMessageDispatcher>();

            services.AddHttpClient<ITelegramBotApiClient, TelegramBotApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
            });

            services.AddSingleton<ITelegramUpdateRouter, TelegramUpdateRouter>();
            services.AddSingleton<ITelegramUpdateHandler, TelegramMessageUpdateHandler>();
            services.AddSingleton<ITelegramMessageMapper, TelegramMessageMapper>();
            services.AddHostedService<TelegramPollingService>();
        }

        // Replace Infrastructure stub crawl strategies with real Host implementations
        var playwrightStub = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IJobCrawlStrategy) &&
            d.ImplementationType?.Name == "PlaywrightJobCrawlStrategy");
        if (playwrightStub != null) services.Remove(playwrightStub);

        var httpStub = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IJobCrawlStrategy) &&
            d.ImplementationType?.Name == "ScriptedHttpJobCrawlStrategy");
        if (httpStub != null) services.Remove(httpStub);

        services.AddScoped<IJobCrawlStrategy, HostPlaywrightJobCrawlStrategy>();
        services.AddScoped<IJobCrawlStrategy, HostScriptedHttpJobCrawlStrategy>();
        services.AddHttpClient("JobCrawler");

        services
            .AddHealthChecks()
            .AddCheck<ConfigurationHealthCheck>("configuration");

        return services;
    }
}
