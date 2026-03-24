using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SandraMaya.Infrastructure;
using SandraMaya.Infrastructure.Persistence;

namespace SandraMaya.Infrastructure.Tests;

public sealed class InfrastructureTestHarness : IAsyncDisposable
{
    private InfrastructureTestHarness(string rootPath, ServiceProvider services)
    {
        RootPath = rootPath;
        Services = services;
    }

    public string RootPath { get; }
    public ServiceProvider Services { get; }

    public static async Task<InfrastructureTestHarness> CreateAsync()
    {
        var rootPath = Path.Combine(AppContext.BaseDirectory, "test-output", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Root"] = "data",
                ["Storage:SqlitePath"] = "data\\memory-tests.db",
                ["Storage:UploadsPath"] = "data\\storage"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(rootPath));
        services.AddMemoryFoundation(configuration);

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IMemorySchemaInitializer>().InitializeAsync();
        return new InfrastructureTestHarness(rootPath, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();

        if (!Directory.Exists(RootPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                await Task.Delay(50);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}

internal sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "SandraMaya.Tests";
    public string ContentRootPath { get; set; } = contentRootPath;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
}
