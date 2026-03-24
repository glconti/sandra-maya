using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SandraMaya.Application.Abstractions;
using SandraMaya.Infrastructure.Documents;
using SandraMaya.Infrastructure.Jobs;
using SandraMaya.Infrastructure.Persistence;
using SandraMaya.Infrastructure.Services;
using SandraMaya.Infrastructure.Storage;

namespace SandraMaya.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddMemoryFoundation(serviceProvider =>
        {
            var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
            return MemoryFoundationPaths.FromConfiguration(configuration, hostEnvironment.ContentRootPath);
        });
    }

    public static IServiceCollection AddMemoryFoundation(
        this IServiceCollection services,
        Func<IServiceProvider, string> sqlitePathFactory,
        Func<IServiceProvider, string> fileStorageRootFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sqlitePathFactory);
        ArgumentNullException.ThrowIfNull(fileStorageRootFactory);

        return services.AddMemoryFoundation(serviceProvider =>
            new MemoryFoundationPaths(
                sqlitePathFactory(serviceProvider),
                fileStorageRootFactory(serviceProvider)));
    }

    private static IServiceCollection AddMemoryFoundation(
        this IServiceCollection services,
        Func<IServiceProvider, MemoryFoundationPaths> pathFactory)
    {
        services.AddSingleton(pathFactory);

        services.AddDbContext<MemoryDbContext>((serviceProvider, optionsBuilder) =>
        {
            var paths = serviceProvider.GetRequiredService<MemoryFoundationPaths>();
            var sqliteDirectory = Path.GetDirectoryName(paths.SqlitePath);

            if (!string.IsNullOrWhiteSpace(sqliteDirectory))
            {
                Directory.CreateDirectory(sqliteDirectory);
            }

            optionsBuilder.UseSqlite($"Data Source={paths.SqlitePath}");
        });

        services.AddScoped<SqliteMemoryStore>();
        services.AddScoped<IMemoryCommandService>(static serviceProvider => serviceProvider.GetRequiredService<SqliteMemoryStore>());
        services.AddScoped<IMemoryQueryService>(static serviceProvider => serviceProvider.GetRequiredService<SqliteMemoryStore>());
        services.AddScoped<ICvIngestionService, CvIngestionService>();
        services.AddScoped<IJobApplicationTrackingService, JobApplicationTrackingService>();
        services.AddScoped<IJobActivityReportingService, JobActivityReportingService>();
        services.AddScoped<ICoverLetterDraftService, PlaceholderCoverLetterDraftService>();
        services.AddScoped<IJobCrawlIngestionService, JobCrawlIngestionService>();
        services.AddScoped<IJobCrawler, JobCrawler>();
        services.AddSingleton<IFileStorage>(static serviceProvider =>
            new LocalFileStorage(serviceProvider.GetRequiredService<MemoryFoundationPaths>()));
        services.AddSingleton<IJobSiteRegistry, JobSiteRegistry>();
        services.AddSingleton<IJobCrawlStrategySelector, JobCrawlStrategySelectionService>();
        services.AddSingleton<JobPostingImportMapper>();
        services.AddSingleton<IJobCrawlStrategy, ScriptedHttpJobCrawlStrategy>();
        services.AddSingleton<IJobCrawlStrategy, PlaywrightJobCrawlStrategy>();
        services.AddSingleton<IPdfToMarkdownConverter, PdfPigMarkdownConverter>();
        services.AddSingleton<IMemorySchemaInitializer, MemorySchemaInitializer>();
        services.AddHostedService<MemorySchemaInitializerHostedService>();

        return services;
    }
}
