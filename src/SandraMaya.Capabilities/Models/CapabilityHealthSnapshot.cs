namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityHealthSnapshot(
    CapabilityHealthStatus Status,
    DateTimeOffset? CheckedAt = null,
    DateTimeOffset? LastSuccessAt = null,
    DateTimeOffset? LastFailureAt = null,
    string? Message = null);
