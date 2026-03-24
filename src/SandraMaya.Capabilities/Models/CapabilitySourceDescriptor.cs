namespace SandraMaya.Capabilities.Models;

public sealed record CapabilitySourceDescriptor(
    CapabilityKind Kind,
    CapabilitySourceType SourceType,
    string? Reference = null,
    string? Revision = null,
    string? CreatedBy = null,
    string? ProvenanceDigest = null);
