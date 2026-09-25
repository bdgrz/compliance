using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// A governed reference in the current draft or an approved boundary version. Historical draft
/// revisions are not retained here; the boundary event history remains their source.
/// </summary>
public sealed record ApplicationBoundaryReferenceView(Uuid TenantId, string SubjectType,
    Uuid GovernedRecordId, Uuid BoundaryId, Uuid ProgramId, Uuid VersionId,
    Uuid EntryId, long Revision, string Status, DateOnly? EffectiveFrom,
    string Kind, string Subject, string OwnerReference, string Rationale);
