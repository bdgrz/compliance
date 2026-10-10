using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A bounded observation, not an approval or a complete retirement clearance.</summary>
public sealed record ApplicationChangePreview(Uuid TenantId, Uuid ApplicationId,
    long ApplicationRevision, string ChangeKind,
    IReadOnlyList<ApplicationFieldChange> Changes,
    IReadOnlyList<ApplicationBoundaryReferenceView> BoundaryReferences,
    IReadOnlyList<string> PendingContexts, bool Complete)
{
    /// <summary>Digest of the normalized, bounded evidence and pending context set.</summary>
    public string ImpactDigest { get; init; } = string.Empty;

    public IReadOnlyList<ApplicationControlDraftReferenceView> ControlDraftReferences { get; init; } = [];

    /// <summary>Up to 200 projected system instances that a change or retirement would affect.</summary>
    public IReadOnlyList<SystemInstanceView> SystemInstanceReferences { get; init; } = [];
}
