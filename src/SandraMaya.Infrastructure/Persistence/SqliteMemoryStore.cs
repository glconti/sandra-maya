using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Infrastructure.Helpers;

namespace SandraMaya.Infrastructure.Persistence;

public sealed class SqliteMemoryStore(MemoryDbContext dbContext) : IMemoryCommandService, IMemoryQueryService
{
    public async Task<UserProfile> SaveUserAsync(UserProfile user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.UserProfiles
            .SingleOrDefaultAsync(
                x => x.Id == user.Id || x.ExternalUserKey == user.ExternalUserKey,
                cancellationToken);

        if (existing is null)
        {
            if (user.Id == Guid.Empty)
            {
                user.Id = Guid.NewGuid();
            }

            user.CreatedAtUtc = now;
            user.UpdatedAtUtc = now;
            dbContext.UserProfiles.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            return user;
        }

        existing.DisplayName = user.DisplayName;
        existing.ExternalUserKey = user.ExternalUserKey;
        existing.PreferredLocale = user.PreferredLocale;
        existing.TimeZone = user.TimeZone;
        existing.ProfileNotesMarkdown = user.ProfileNotesMarkdown;
        existing.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<AssistantProfileState> SaveAssistantProfileStateAsync(AssistantProfileState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var existing = await dbContext.AssistantProfileStates
            .SingleOrDefaultAsync(x => x.UserProfileId == state.UserProfileId, cancellationToken);

        if (existing is null)
        {
            if (state.Id == Guid.Empty)
            {
                state.Id = Guid.NewGuid();
            }

            state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            dbContext.AssistantProfileStates.Add(state);
            await dbContext.SaveChangesAsync(cancellationToken);
            return state;
        }

        existing.DisplayPersona = state.DisplayPersona;
        existing.GoalsSummary = state.GoalsSummary;
        existing.PreferencesJson = state.PreferencesJson;
        existing.RetrievalHints = state.RetrievalHints;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<StoredAsset> SaveAssetMetadataAsync(StoredAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var existing = await dbContext.StoredAssets
            .SingleOrDefaultAsync(x => x.Id == asset.Id, cancellationToken);

        if (existing is null)
        {
            if (asset.Id == Guid.Empty)
            {
                asset.Id = Guid.NewGuid();
            }

            asset.StoragePath = asset.StoragePath.Replace('/', Path.DirectorySeparatorChar);
            asset.CreatedAtUtc = asset.CreatedAtUtc == default ? DateTimeOffset.UtcNow : asset.CreatedAtUtc;
            dbContext.StoredAssets.Add(asset);
            await dbContext.SaveChangesAsync(cancellationToken);
            return asset;
        }

        existing.Partition = asset.Partition;
        existing.Role = asset.Role;
        existing.DerivedFromAssetId = asset.DerivedFromAssetId;
        existing.OriginalFileName = asset.OriginalFileName;
        existing.ContentType = asset.ContentType;
        existing.StoragePath = asset.StoragePath.Replace('/', Path.DirectorySeparatorChar);
        existing.ByteCount = asset.ByteCount;
        existing.Sha256 = asset.Sha256;
        existing.MetadataJson = asset.MetadataJson;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<MarkdownDocument> SaveMarkdownDocumentAsync(MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.PlainTextContent = string.IsNullOrWhiteSpace(document.PlainTextContent)
            ? MemoryTextNormalizer.MarkdownToPlainText(document.MarkdownContent)
            : MemoryTextNormalizer.NormalizeExtractedText(document.PlainTextContent);

        if (string.IsNullOrWhiteSpace(document.NormalizedHash))
        {
            document.NormalizedHash = MemoryTextNormalizer.ComputeSha256(document.PlainTextContent);
        }

        var existing = await dbContext.MarkdownDocuments
            .SingleOrDefaultAsync(x => x.Id == document.Id, cancellationToken);

        if (existing is null)
        {
            if (document.Id == Guid.Empty)
            {
                document.Id = Guid.NewGuid();
            }

            var now = DateTimeOffset.UtcNow;
            document.CreatedAtUtc = document.CreatedAtUtc == default ? now : document.CreatedAtUtc;
            document.UpdatedAtUtc = now;
            dbContext.MarkdownDocuments.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);
            return document;
        }

        existing.SourceAssetId = document.SourceAssetId;
        existing.ArtifactAssetId = document.ArtifactAssetId;
        existing.Kind = document.Kind;
        existing.Title = document.Title;
        existing.MarkdownContent = document.MarkdownContent;
        existing.PlainTextContent = document.PlainTextContent;
        existing.NormalizedHash = document.NormalizedHash;
        existing.MetadataJson = document.MetadataJson;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<StructuredProfileSnapshot> SaveStructuredProfileSnapshotAsync(StructuredProfileSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.IsCurrent)
        {
            var currentSnapshots = await dbContext.StructuredProfileSnapshots
                .Where(x => x.UserProfileId == snapshot.UserProfileId && x.IsCurrent)
                .ToListAsync(cancellationToken);

            foreach (var current in currentSnapshots)
            {
                current.IsCurrent = false;
            }
        }

        if (snapshot.Id == Guid.Empty)
        {
            snapshot.Id = Guid.NewGuid();
        }

        snapshot.CreatedAtUtc = snapshot.CreatedAtUtc == default ? DateTimeOffset.UtcNow : snapshot.CreatedAtUtc;
        dbContext.StructuredProfileSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task<CvRevision> SaveCvRevisionAsync(CvRevision revision, bool makeCanonical = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);

        if (makeCanonical || revision.IsCanonical)
        {
            var canonicalRevisions = await dbContext.CvRevisions
                .Where(x => x.UserProfileId == revision.UserProfileId && x.IsCanonical)
                .ToListAsync(cancellationToken);

            foreach (var canonical in canonicalRevisions)
            {
                canonical.IsCanonical = false;
            }

            revision.IsCanonical = true;
        }

        if (revision.RevisionNumber <= 0)
        {
            var latestNumber = await dbContext.CvRevisions
                .Where(x => x.UserProfileId == revision.UserProfileId)
                .Select(x => (int?)x.RevisionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            revision.RevisionNumber = latestNumber + 1;
        }

        if (revision.Id == Guid.Empty)
        {
            revision.Id = Guid.NewGuid();
        }

        revision.UploadedAtUtc = revision.UploadedAtUtc == default ? DateTimeOffset.UtcNow : revision.UploadedAtUtc;
        dbContext.CvRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task<JobPosting> UpsertJobPostingAsync(JobPosting posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);

        posting.NormalizedTitle = MemoryTextNormalizer.NormalizeKeyPart(posting.Title);
        posting.NormalizedCompany = MemoryTextNormalizer.NormalizeKeyPart(posting.CompanyName);
        posting.DedupeKey = string.IsNullOrWhiteSpace(posting.DedupeKey)
            ? MemoryTextNormalizer.BuildJobPostingDedupeKey(posting)
            : posting.DedupeKey;

        var existing = await dbContext.JobPostings
            .SingleOrDefaultAsync(
                x => x.UserProfileId == posting.UserProfileId && x.DedupeKey == posting.DedupeKey,
                cancellationToken);

        if (existing is null)
        {
            if (posting.Id == Guid.Empty)
            {
                posting.Id = Guid.NewGuid();
            }

            posting.FirstSeenAtUtc = posting.FirstSeenAtUtc == default ? DateTimeOffset.UtcNow : posting.FirstSeenAtUtc;
            posting.LastSeenAtUtc = posting.LastSeenAtUtc == default ? posting.FirstSeenAtUtc : posting.LastSeenAtUtc;
            dbContext.JobPostings.Add(posting);
            await dbContext.SaveChangesAsync(cancellationToken);
            return posting;
        }

        existing.SourceSite = posting.SourceSite;
        existing.SourcePostingId = posting.SourcePostingId;
        existing.SourceUrl = posting.SourceUrl;
        existing.Title = posting.Title;
        existing.NormalizedTitle = posting.NormalizedTitle;
        existing.CompanyName = posting.CompanyName;
        existing.NormalizedCompany = posting.NormalizedCompany;
        existing.Location = posting.Location;
        existing.EmploymentType = posting.EmploymentType;
        existing.CompensationText = posting.CompensationText;
        existing.DescriptionDocumentId = posting.DescriptionDocumentId;
        existing.RawPayloadJson = posting.RawPayloadJson;
        existing.IsActive = posting.IsActive;
        existing.LastSeenAtUtc = posting.LastSeenAtUtc == default ? DateTimeOffset.UtcNow : posting.LastSeenAtUtc;
        existing.PostedAtUtc = posting.PostedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<JobApplicationStatusRecord> SaveJobApplicationStatusAsync(
        JobApplicationStatusRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.JobApplicationStatusRecords
            .SingleOrDefaultAsync(
                x => x.Id == record.Id || (x.UserProfileId == record.UserProfileId && x.JobPostingId == record.JobPostingId),
                cancellationToken);

        if (existing is null)
        {
            if (record.Id == Guid.Empty)
            {
                record.Id = Guid.NewGuid();
            }

            record.CreatedAtUtc = record.CreatedAtUtc == default ? now : record.CreatedAtUtc;
            record.UpdatedAtUtc = record.UpdatedAtUtc == default ? now : record.UpdatedAtUtc;

            if (record.Status == JobApplicationStatus.Applied && record.AppliedAtUtc is null)
            {
                record.AppliedAtUtc = now;
            }

            dbContext.JobApplicationStatusRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return record;
        }

        existing.CvRevisionId = record.CvRevisionId;
        existing.Status = record.Status;
        existing.NotesMarkdown = record.NotesMarkdown;
        existing.MetadataJson = record.MetadataJson;
        existing.AppliedAtUtc = record.Status == JobApplicationStatus.Applied
            ? record.AppliedAtUtc ?? existing.AppliedAtUtc ?? now
            : record.AppliedAtUtc ?? existing.AppliedAtUtc;
        existing.UpdatedAtUtc = record.UpdatedAtUtc == default ? now : record.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<AssistantProfileState?> GetAssistantProfileStateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.AssistantProfileStates
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserProfileId == userId, cancellationToken);

    public async Task<IReadOnlyList<StoredAsset>> GetAssetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var assets = await dbContext.StoredAssets
            .AsNoTracking()
            .Where(x => x.UserProfileId == userId)
            .ToListAsync(cancellationToken);

        return assets
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CvRevision>> GetCvRevisionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.CvRevisions
            .AsNoTracking()
            .Include(x => x.MarkdownDocument)
            .Include(x => x.SourceAsset)
            .Where(x => x.UserProfileId == userId)
            .OrderByDescending(x => x.RevisionNumber)
            .ToListAsync(cancellationToken);

    public Task<CvRevision?> GetCanonicalCvRevisionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.CvRevisions
            .AsNoTracking()
            .Include(x => x.MarkdownDocument)
            .Include(x => x.SourceAsset)
            .Where(x => x.UserProfileId == userId && x.IsCanonical)
            .OrderByDescending(x => x.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<StructuredProfileSnapshot?> GetLatestStructuredProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var snapshots = await dbContext.StructuredProfileSnapshots
            .AsNoTracking()
            .Where(x => x.UserProfileId == userId)
            .ToListAsync(cancellationToken);

        return snapshots
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchDocumentsAsync(Guid userId, string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        var ftsQuery = MemoryTextNormalizer.BuildFtsQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery))
        {
            return Array.Empty<MarkdownDocumentSearchResult>();
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT d.Id,
                       d.Title,
                       d.Kind,
                       snippet(MarkdownDocumentSearch, 3, '[', ']', ' … ', 18) AS Snippet,
                       bm25(MarkdownDocumentSearch) AS Rank
                FROM MarkdownDocumentSearch
                INNER JOIN MarkdownDocuments AS d ON d.Id = MarkdownDocumentSearch.DocumentId
                WHERE MarkdownDocumentSearch MATCH $ftsQuery
                  AND d.UserProfileId = $userId
                ORDER BY Rank
                LIMIT $limit;
                """;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "$userId";
            userIdParameter.Value = userId.ToString();
            command.Parameters.Add(userIdParameter);

            var queryParameter = command.CreateParameter();
            queryParameter.ParameterName = "$ftsQuery";
            queryParameter.Value = ftsQuery;
            command.Parameters.Add(queryParameter);

            var limitParameter = command.CreateParameter();
            limitParameter.ParameterName = "$limit";
            limitParameter.Value = Math.Clamp(limit, 1, 100);
            command.Parameters.Add(limitParameter);

            var results = new List<MarkdownDocumentSearchResult>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = Guid.Parse(reader.GetString(0));
                var title = reader.GetString(1);
                var kindValue = reader.GetString(2);
                var snippet = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var rank = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);
                var kind = Enum.TryParse<DocumentKind>(kindValue, ignoreCase: true, out var parsedKind)
                    ? parsedKind
                    : DocumentKind.Unknown;

                results.Add(new MarkdownDocumentSearchResult(id, title, kind, snippet, rank));
            }

            if (results.Count > 0)
            {
                return results;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var fallbackCandidates = await dbContext.MarkdownDocuments
            .AsNoTracking()
            .Where(x => x.UserProfileId == userId)
            .ToListAsync(cancellationToken);

        var fallbackTokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return fallbackCandidates
            .Where(document => fallbackTokens.All(token =>
                document.Title.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                document.PlainTextContent.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Clamp(limit, 1, 100))
            .Select(document => new MarkdownDocumentSearchResult(
                document.Id,
                document.Title,
                document.Kind,
                document.PlainTextContent.Length > 160
                    ? document.PlainTextContent[..160]
                    : document.PlainTextContent,
                0d))
            .ToList();
    }

    public async Task<IReadOnlyList<JobPosting>> SearchJobPostingsAsync(Guid userId, JobPostingQuery query, CancellationToken cancellationToken = default)
    {
        var postings = dbContext.JobPostings
            .AsNoTracking()
            .Where(x => x.UserProfileId == userId);

        if (query.ActiveOnly)
        {
            postings = postings.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceSite))
        {
            postings = postings.Where(x => x.SourceSite == query.SourceSite);
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            postings = postings.Where(x => x.Location.Contains(query.Location));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            var normalizedSearch = MemoryTextNormalizer.NormalizeKeyPart(searchText);
            postings = postings.Where(x =>
                x.Title.Contains(searchText) ||
                x.CompanyName.Contains(searchText) ||
                x.Location.Contains(searchText) ||
                x.NormalizedTitle.Contains(normalizedSearch) ||
                x.NormalizedCompany.Contains(normalizedSearch));
        }

        var materialized = await postings.ToListAsync(cancellationToken);
        return materialized
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(Math.Clamp(query.Limit, 1, 100))
            .ToList();
    }

    public async Task<IReadOnlyList<JobPosting>> GetJobPostingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var postings = await dbContext.JobPostings
            .AsNoTracking()
            .Include(x => x.DescriptionDocument)
            .Where(x => x.UserProfileId == userId)
            .ToListAsync(cancellationToken);

        return postings
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToList();
    }

    public Task<JobPosting?> GetJobPostingAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default) =>
        dbContext.JobPostings
            .AsNoTracking()
            .Include(x => x.DescriptionDocument)
            .SingleOrDefaultAsync(x => x.UserProfileId == userId && x.Id == jobPostingId, cancellationToken);

    public Task<JobPosting?> GetJobPostingByDedupeKeyAsync(Guid userId, string dedupeKey, CancellationToken cancellationToken = default) =>
        dbContext.JobPostings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserProfileId == userId && x.DedupeKey == dedupeKey, cancellationToken);

    public Task<JobApplicationStatusRecord?> GetJobApplicationStatusAsync(
        Guid userId,
        Guid jobPostingId,
        CancellationToken cancellationToken = default) =>
        dbContext.JobApplicationStatusRecords
            .AsNoTracking()
            .Include(x => x.JobPosting)
            .Include(x => x.CvRevision)
            .SingleOrDefaultAsync(
                x => x.UserProfileId == userId && x.JobPostingId == jobPostingId,
                cancellationToken);

    public async Task<IReadOnlyList<JobApplicationStatusRecord>> GetJobApplicationStatusesAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        (await dbContext.JobApplicationStatusRecords
            .AsNoTracking()
            .Include(x => x.JobPosting)
            .Include(x => x.CvRevision)
            .Where(x => x.UserProfileId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();
}
