using Microsoft.Extensions.Options;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Configuration;
using SandraMaya.Capabilities.Persistence;
using SandraMaya.Capabilities.Services;
using SandraMaya.Infrastructure;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Health;
using SandraMaya.Host.Storage;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSandraMayaHost(this IServiceCollection services, IConfiguration configuration)
    {
        var telegramOptions = configuration.GetSection(TelegramOptions.SectionName).Get<TelegramOptions>() ?? new TelegramOptions();

        services.AddMemoryFoundation(configuration);

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

        services.AddSingleton<IAssistantSessionStore, InMemoryAssistantSessionStore>();
        services.AddSingleton<IAssistantOrchestrator, AzureOpenAiAssistantOrchestrator>();
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

        services
            .AddHealthChecks()
            .AddCheck<ConfigurationHealthCheck>("configuration");

        return services;
    }
}
