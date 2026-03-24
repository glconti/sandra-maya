using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SandraMaya.Infrastructure.Persistence;

public interface IMemorySchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class MemorySchemaInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<MemorySchemaInitializer> logger) : IMemorySchemaInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS MarkdownDocumentSearch USING fts5(
                DocumentId UNINDEXED,
                UserProfileId UNINDEXED,
                Title,
                PlainText
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS MarkdownDocuments_AfterInsert
            AFTER INSERT ON MarkdownDocuments
            BEGIN
                INSERT INTO MarkdownDocumentSearch (DocumentId, UserProfileId, Title, PlainText)
                VALUES (new.Id, new.UserProfileId, new.Title, new.PlainTextContent);
            END;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS MarkdownDocuments_AfterUpdate
            AFTER UPDATE ON MarkdownDocuments
            BEGIN
                DELETE FROM MarkdownDocumentSearch WHERE DocumentId = old.Id;
                INSERT INTO MarkdownDocumentSearch (DocumentId, UserProfileId, Title, PlainText)
                VALUES (new.Id, new.UserProfileId, new.Title, new.PlainTextContent);
            END;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS MarkdownDocuments_AfterDelete
            AFTER DELETE ON MarkdownDocuments
            BEGIN
                DELETE FROM MarkdownDocumentSearch WHERE DocumentId = old.Id;
            END;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO MarkdownDocumentSearch (DocumentId, UserProfileId, Title, PlainText)
            SELECT d.Id, d.UserProfileId, d.Title, d.PlainTextContent
            FROM MarkdownDocuments AS d
            WHERE NOT EXISTS (
                SELECT 1
                FROM MarkdownDocumentSearch AS s
                WHERE s.DocumentId = d.Id
            );
            """,
            cancellationToken);

        logger.LogInformation("Memory schema initialized.");
    }
}

public sealed class MemorySchemaInitializerHostedService(
    IMemorySchemaInitializer schemaInitializer,
    ILogger<MemorySchemaInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await schemaInitializer.InitializeAsync(cancellationToken);
        logger.LogInformation("Memory schema initialization hosted service completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
