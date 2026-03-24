namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityTimestamps(
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? InstalledAt = null,
    DateTimeOffset? EnabledAt = null,
    DateTimeOffset? DisabledAt = null,
    DateTimeOffset? RemovedAt = null,
    DateTimeOffset? LastExecutedAt = null);
