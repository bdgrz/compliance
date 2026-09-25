using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// A current Control draft relationship. It is not an approved ControlVersion, activation,
/// historical relationship, or complete application-change impact result.
/// </summary>
public sealed record ApplicationControlDraftReferenceView(Uuid TenantId, string SubjectType,
    Uuid GovernedRecordId, Uuid ProgramId, Uuid ControlId, string Identifier,
    long Revision, Uuid EntryId, string Subject, string Rationale);
