using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryScopeEntry(Uuid EntryId, string Kind, string SubjectType,
    string Subject, Uuid? GovernedRecordId, string OwnerReference,
    string Rationale, bool Unresolved);
