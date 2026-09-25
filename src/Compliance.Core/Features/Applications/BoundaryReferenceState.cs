using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Keys needed to replace current references without scanning a tenant's boundaries.</summary>
sealed record BoundaryReferenceState(Uuid BoundaryId, Uuid ProgramId,
    Uuid? DraftVersionId, long DraftRevision, IReadOnlyList<Uuid> DraftEntryIds,
    Uuid? ApprovedVersionId, IReadOnlyList<Uuid> ApprovedEntryIds);
