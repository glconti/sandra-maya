using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Persistence;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AssistantProfileState> AssistantProfileStates => Set<AssistantProfileState>();
    public DbSet<StoredAsset> StoredAssets => Set<StoredAsset>();
    public DbSet<MarkdownDocument> MarkdownDocuments => Set<MarkdownDocument>();
    public DbSet<CvRevision> CvRevisions => Set<CvRevision>();
    public DbSet<StructuredProfileSnapshot> StructuredProfileSnapshots => Set<StructuredProfileSnapshot>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplicationStatusRecord> JobApplicationStatusRecords => Set<JobApplicationStatusRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var storagePartitionConverter = new EnumToStringConverter<StoragePartition>();
        var assetRoleConverter = new EnumToStringConverter<AssetRole>();
        var documentKindConverter = new EnumToStringConverter<DocumentKind>();
        var jobApplicationStatusConverter = new EnumToStringConverter<JobApplicationStatus>();
        var dateTimeOffsetConverter = new DateTimeOffsetToStringConverter();
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, string?>(
            value => value.HasValue ? value.Value.ToString("O", CultureInfo.InvariantCulture) : null,
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.ExternalUserKey).HasMaxLength(200).IsRequired();
            entity.Property(static x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(static x => x.PreferredLocale).HasMaxLength(32).IsRequired();
            entity.Property(static x => x.TimeZone).HasMaxLength(128).IsRequired();
            entity.Property(static x => x.CreatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.Property(static x => x.UpdatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => x.ExternalUserKey).IsUnique();
        });

        modelBuilder.Entity<AssistantProfileState>(entity =>
        {
            entity.ToTable("AssistantProfileStates");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.DisplayPersona).HasMaxLength(200).IsRequired();
            entity.Property(static x => x.UpdatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => x.UserProfileId).IsUnique();
            entity.HasOne(static x => x.UserProfile)
                .WithOne(static x => x.AssistantProfileState)
                .HasForeignKey<AssistantProfileState>(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoredAsset>(entity =>
        {
            entity.ToTable("StoredAssets");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.Partition).HasConversion(storagePartitionConverter).HasMaxLength(32);
            entity.Property(static x => x.Role).HasConversion(assetRoleConverter).HasMaxLength(64);
            entity.Property(static x => x.OriginalFileName).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.ContentType).HasMaxLength(256).IsRequired();
            entity.Property(static x => x.StoragePath).HasMaxLength(1024).IsRequired();
            entity.Property(static x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(static x => x.MetadataJson).HasDefaultValue("{}");
            entity.Property(static x => x.CreatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.CreatedAtUtc });
            entity.HasIndex(static x => new { x.UserProfileId, x.Sha256 });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.Assets)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.DerivedFromAsset)
                .WithMany(static x => x.DerivedAssets)
                .HasForeignKey(static x => x.DerivedFromAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MarkdownDocument>(entity =>
        {
            entity.ToTable("MarkdownDocuments");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.Kind).HasConversion(documentKindConverter).HasMaxLength(64);
            entity.Property(static x => x.Title).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.MarkdownContent).IsRequired();
            entity.Property(static x => x.PlainTextContent).IsRequired();
            entity.Property(static x => x.NormalizedHash).HasMaxLength(64).IsRequired();
            entity.Property(static x => x.MetadataJson).HasDefaultValue("{}");
            entity.Property(static x => x.CreatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.Property(static x => x.UpdatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.CreatedAtUtc });
            entity.HasIndex(static x => new { x.UserProfileId, x.NormalizedHash });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.MarkdownDocuments)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.SourceAsset)
                .WithMany()
                .HasForeignKey(static x => x.SourceAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(static x => x.ArtifactAsset)
                .WithMany()
                .HasForeignKey(static x => x.ArtifactAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CvRevision>(entity =>
        {
            entity.ToTable("CvRevisions");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.Summary).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.UploadedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.RevisionNumber }).IsUnique();
            entity.HasIndex(static x => new { x.UserProfileId, x.IsCanonical });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.CvRevisions)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.SourceAsset)
                .WithMany()
                .HasForeignKey(static x => x.SourceAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(static x => x.MarkdownDocument)
                .WithMany()
                .HasForeignKey(static x => x.MarkdownDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StructuredProfileSnapshot>(entity =>
        {
            entity.ToTable("StructuredProfileSnapshots");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.SchemaVersion).HasMaxLength(32).IsRequired();
            entity.Property(static x => x.JsonPayload).IsRequired();
            entity.Property(static x => x.CreatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.CreatedAtUtc });
            entity.HasIndex(static x => new { x.UserProfileId, x.IsCurrent });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.StructuredProfiles)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.SourceCvRevision)
                .WithMany()
                .HasForeignKey(static x => x.SourceCvRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(static x => x.SourceDocument)
                .WithMany()
                .HasForeignKey(static x => x.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.ToTable("JobPostings");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.SourceSite).HasMaxLength(128).IsRequired();
            entity.Property(static x => x.SourcePostingId).HasMaxLength(256);
            entity.Property(static x => x.SourceUrl).HasMaxLength(2048).IsRequired();
            entity.Property(static x => x.DedupeKey).HasMaxLength(64).IsRequired();
            entity.Property(static x => x.Title).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.NormalizedTitle).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.CompanyName).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.NormalizedCompany).HasMaxLength(512).IsRequired();
            entity.Property(static x => x.Location).HasMaxLength(256).IsRequired();
            entity.Property(static x => x.EmploymentType).HasMaxLength(128).IsRequired();
            entity.Property(static x => x.CompensationText).HasMaxLength(256).IsRequired();
            entity.Property(static x => x.RawPayloadJson).IsRequired();
            entity.Property(static x => x.FirstSeenAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.Property(static x => x.LastSeenAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.Property(static x => x.PostedAtUtc).HasConversion(nullableDateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.DedupeKey }).IsUnique();
            entity.HasIndex(static x => new { x.UserProfileId, x.SourceSite, x.LastSeenAtUtc });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.JobPostings)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.DescriptionDocument)
                .WithMany()
                .HasForeignKey(static x => x.DescriptionDocumentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JobApplicationStatusRecord>(entity =>
        {
            entity.ToTable("JobApplicationStatusRecords");
            entity.HasKey(static x => x.Id);
            entity.Property(static x => x.Status).HasConversion(jobApplicationStatusConverter).HasMaxLength(64);
            entity.Property(static x => x.NotesMarkdown).IsRequired();
            entity.Property(static x => x.MetadataJson).HasDefaultValue("{}");
            entity.Property(static x => x.UpdatedAtUtc).HasConversion(dateTimeOffsetConverter);
            entity.HasIndex(static x => new { x.UserProfileId, x.JobPostingId }).IsUnique();
            entity.HasIndex(static x => new { x.UserProfileId, x.UpdatedAtUtc });
            entity.HasOne(static x => x.UserProfile)
                .WithMany(static x => x.JobApplicationStatuses)
                .HasForeignKey(static x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.JobPosting)
                .WithMany(static x => x.ApplicationStatuses)
                .HasForeignKey(static x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(static x => x.CvRevision)
                .WithMany()
                .HasForeignKey(static x => x.CvRevisionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
