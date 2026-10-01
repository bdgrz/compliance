using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Field names only; restricted conflicts reveal neither their values nor their field names.</summary>
public sealed record WorkforceSourcePreview(Uuid ObservationId, long Revision,
    WorkforceSourceIdentity Source, string TargetKind, Uuid TargetId,
    long ObservedTargetRevision, long CurrentTargetRevision, string SourceAuthority,
    IReadOnlyList<string> ConflictingFields, bool RestrictedFieldsConflict, bool CanAccept,
    bool AcceptedForCurrentRevision, WorkforceSourceDecision? Decision);
