using Microsoft.Extensions.Options;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace SandraMaya.Host.Tests;

public sealed class CopilotRuntimeConfigurationTests
{
    [Fact]
    public void TryResolve_UsesByokSettings_WhenAzureConfigurationIsPresent()
    {
        var subject = CreateSubject(
            new AzureOpenAiOptions
            {
                ProviderType = "azure",
                BaseUrl = "https://example.openai.azure.com",
                ApiKey = "test-key",
                DeploymentName = "gpt-4.1",
                ApiVersion = "2024-10-21"
            },
            new CopilotRuntimeOptions
            {
                Model = "should-not-be-used"
            });

        var resolved = subject.TryResolve(out var settings, out var errorMessage);

        Assert.True(resolved);
        Assert.Equal(string.Empty, errorMessage);
        Assert.True(settings.UsesByokProvider);
        Assert.Equal("gpt-4.1", settings.Model);
        Assert.NotNull(settings.Provider);
        Assert.Equal("azure", settings.Provider!.Type);
        Assert.Equal("https://example.openai.azure.com", settings.Provider.BaseUrl);
        Assert.Equal("test-key", settings.Provider.ApiKey);
        Assert.Equal("2024-10-21", settings.Provider.Azure!.ApiVersion);
    }

    [Fact]
    public void TryResolve_UsesCopilotModel_WhenByokIsAbsent()
    {
        var subject = CreateSubject(
            new AzureOpenAiOptions(),
            new CopilotRuntimeOptions
            {
                Model = "gpt-5-mini",
                WorkingDirectory = @"D:\Repos\sandra-maya.copilot-sdk",
                ClientName = "Test Client"
            });

        var resolved = subject.TryResolve(out var settings, out var errorMessage);

        Assert.True(resolved);
        Assert.Equal(string.Empty, errorMessage);
        Assert.False(settings.UsesByokProvider);
        Assert.Equal("gpt-5-mini", settings.Model);
        Assert.Null(settings.Provider);
        Assert.Equal(@"D:\Repos\sandra-maya.copilot-sdk", settings.WorkingDirectory);
        Assert.Equal("Test Client", settings.ClientName);
    }

    [Fact]
    public void TryResolve_ReturnsHelpfulError_WhenNoModelSourceIsConfigured()
    {
        var subject = CreateSubject(new AzureOpenAiOptions(), new CopilotRuntimeOptions());

        var resolved = subject.TryResolve(out _, out var errorMessage);

        Assert.False(resolved);
        Assert.Contains("Copilot runtime is not configured", errorMessage, StringComparison.Ordinal);
    }

    private static CopilotRuntimeConfiguration CreateSubject(
        AzureOpenAiOptions azureOptions,
        CopilotRuntimeOptions copilotOptions)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "sandra-maya-host-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        return new CopilotRuntimeConfiguration(
            Options.Create(azureOptions),
            Options.Create(copilotOptions),
            new TestHostEnvironment(contentRoot));
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SandraMaya.Host.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
