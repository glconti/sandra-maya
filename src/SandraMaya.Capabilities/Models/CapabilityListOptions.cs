namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityListOptions(
    CapabilityKind? Kind = null,
    CapabilityStatus? Status = null,
    bool IncludeRemoved = false);
