using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryImpactPreview(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, Uuid? ApprovedVersionId,
    IReadOnlyList<BoundaryChange> Changes,
    IReadOnlyList<BoundaryImpactContribution> Contributions,
    IReadOnlyList<string> PendingContexts, bool Complete, string Digest);
