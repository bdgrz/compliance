using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public static class ResponsibilityDecisionGuard
{
    public static CommandFailure? Validate(ResponsibilitySet set, ResponsibilityScope scope,
        Uuid memberId, ResponsibilityType decisionType, DateTimeOffset at,
        bool isRecordAuthor, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (decisionType is not (ResponsibilityType.AssignedReviewer or
            ResponsibilityType.PolicyApprover))
            return CommandFailure.InvalidContent(
                "A responsibility decision must be a review or policy approval.");
        var proposed = new ResponsibilityAssignmentView(set.TenantId, Uuid.Empty, memberId,
            decisionType, scope, at, Uuid.Empty, at, at.AddTicks(1), null, Uuid.Empty, []);
        var conflicts = ResponsibilityConflictPolicy.FindConflicts(set.ReadAssignments(), proposed);
        var action = decisionType == ResponsibilityType.AssignedReviewer
            ? SeparationOfDutiesActions.Review
            : SeparationOfDutiesActions.Approve;
        if (conflicts.Count > 0)
        {
            var exactScope = new SeparationOfDutiesWaiverScope(scope.RecordType, scope.RecordId,
                scope.VersionId, scope.Revision, action);
            return waiver?.TenantId == set.TenantId && waiver.Allows(exactScope, memberId, at)
                ? null
                : CommandFailure.ActorProhibited(
                    "The member has a conflicting responsibility and requires an active exact-scope waiver.");
        }
        return isRecordAuthor || waiver is null
            ? null
            : CommandFailure.ActorProhibited(
                "A separation-of-duties waiver may only be used for a current conflict.");
    }
}
