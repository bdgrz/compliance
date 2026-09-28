using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed record ResponsibilityConflictView(ResponsibilityConflictKind Kind,
    Uuid MemberId, ResponsibilityScope Scope, Uuid ExistingAssignmentId,
    Uuid ProposedAssignmentId, ResponsibilityType ExistingType,
    ResponsibilityType ProposedType, string WaiverAction);
