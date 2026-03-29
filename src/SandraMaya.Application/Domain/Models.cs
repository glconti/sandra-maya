using System.Collections.Generic;

namespace SandraMaya.Application.Domain;

public enum StoragePartition
{
    Uploads = 1,
    Artifacts = 2
}

public enum AssetRole
{
    Unknown = 0,
    CvUpload = 1,
    MarkdownArtifact = 2,
    StructuredProfileArtifact = 3,
    JobPostingArtifact = 4,
    UserUpload = 5
}

public enum DocumentKind
{
    Unknown = 0,
    Cv = 1,
    JobPosting = 2,
    UserNotes = 3,
    ExtractedMarkdown = 4
}

public enum JobApplicationStatus
{
    Draft = 0,
    Saved = 1,
    Applied = 2,
    Interviewing = 3,
    Offer = 4,
    Rejected = 5,
    Withdrawn = 6,
    Archived = 7,
    Interested = 8
}

public sealed class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalUserKey { get; set; } = "default";
    public string DisplayName { get; set; } = "Owner";
    public string PreferredLocale { get; set; } = "en";
    public string TimeZone { get; set; } = "UTC";
    public string ProfileNotesMarkdown { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AssistantProfileState? AssistantProfileState { get; set; }
    public ICollection<StoredAsset> Assets { get; } = new List<StoredAsset>();
    public ICollection<MarkdownDocument> MarkdownDocuments { get; } = new List<MarkdownDocument>();
    public ICollection<CvRevision> CvRevisions { get; } = new List<CvRevision>();
    public ICollection<StructuredProfileSnapshot> StructuredProfiles { get; } = new List<StructuredProfileSnapshot>();
    public ICollection<JobPosting> JobPostings { get; } = new List<JobPosting>();
    public ICollection<JobApplicationStatusRecord> JobApplicationStatuses { get; } = new List<JobApplicationStatusRecord>();
}

public sealed class AssistantProfileState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public string DisplayPersona { get; set; } = "Maya";
    public string GoalsSummary { get; set; } = string.Empty;
    public string PreferencesJson { get; set; } = "{}";
    public string RetrievalHints { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile UserProfile { get; set; } = null!;
}

public sealed class StoredAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public StoragePartition Partition { get; set; } = StoragePartition.Uploads;
    public AssetRole Role { get; set; } = AssetRole.Unknown;
    public Guid? DerivedFromAssetId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string StoragePath { get; set; } = string.Empty;
    public long ByteCount { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile UserProfile { get; set; } = null!;
    public StoredAsset? DerivedFromAsset { get; set; }
    public ICollection<StoredAsset> DerivedAssets { get; } = new List<StoredAsset>();
}

public sealed class MarkdownDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public Guid? SourceAssetId { get; set; }
    public Guid? ArtifactAssetId { get; set; }
    public DocumentKind Kind { get; set; } = DocumentKind.Unknown;
    public string Title { get; set; } = string.Empty;
    public string MarkdownContent { get; set; } = string.Empty;
    public string PlainTextContent { get; set; } = string.Empty;
    public string NormalizedHash { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile UserProfile { get; set; } = null!;
    public StoredAsset? SourceAsset { get; set; }
    public StoredAsset? ArtifactAsset { get; set; }
}

public sealed class CvRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public Guid SourceAssetId { get; set; }
    public Guid MarkdownDocumentId { get; set; }
    public int RevisionNumber { get; set; }
    public bool IsCanonical { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile UserProfile { get; set; } = null!;
    public StoredAsset SourceAsset { get; set; } = null!;
    public MarkdownDocument MarkdownDocument { get; set; } = null!;
}

public sealed class StructuredProfileSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public Guid? SourceCvRevisionId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public bool IsCurrent { get; set; }
    public string SchemaVersion { get; set; } = "v1";
    public string JsonPayload { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile UserProfile { get; set; } = null!;
    public CvRevision? SourceCvRevision { get; set; }
    public MarkdownDocument? SourceDocument { get; set; }
}

public sealed class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public string SourceSite { get; set; } = string.Empty;
    public string? SourcePostingId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string NormalizedCompany { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string CompensationText { get; set; } = string.Empty;
    public Guid? DescriptionDocumentId { get; set; }
    public string RawPayloadJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset FirstSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PostedAtUtc { get; set; }

    public UserProfile UserProfile { get; set; } = null!;
    public MarkdownDocument? DescriptionDocument { get; set; }
    public ICollection<JobApplicationStatusRecord> ApplicationStatuses { get; } = new List<JobApplicationStatusRecord>();
}

public sealed class JobApplicationStatusRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid? CvRevisionId { get; set; }
    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Draft;
    public string NotesMarkdown { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAtUtc { get; set; }

    public UserProfile UserProfile { get; set; } = null!;
    public JobPosting JobPosting { get; set; } = null!;
    public CvRevision? CvRevision { get; set; }
}
