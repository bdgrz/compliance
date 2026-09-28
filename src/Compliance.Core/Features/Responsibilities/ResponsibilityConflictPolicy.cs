using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public static class ResponsibilityConflictPolicy
{
    public static IReadOnlyList<ResponsibilityConflictView> FindConflicts(
        IEnumerable<ResponsibilityAssignmentView> assignments,
        ResponsibilityAssignmentView proposed)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(proposed);

        return assignments
            .Where(existing => existing.TenantId == proposed.TenantId &&
                existing.MemberId == proposed.MemberId && existing.Scope == proposed.Scope &&
                existing.AssignmentId != proposed.AssignmentId &&
                Overlaps(existing, proposed) &&
                TryClassify(existing.Type, proposed.Type, out _, out _))
            .Select(existing =>
            {
                _ = TryClassify(existing.Type, proposed.Type, out var kind, out var action);
                return new ResponsibilityConflictView(kind, proposed.MemberId, proposed.Scope,
                    existing.AssignmentId, proposed.AssignmentId, existing.Type, proposed.Type, action);
            })
            .OrderBy(conflict => conflict.Kind)
            .ThenBy(conflict => conflict.ExistingAssignmentId.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    static bool Overlaps(ResponsibilityAssignmentView left, ResponsibilityAssignmentView right)
    {
        if (left.RevokedAt is { } leftRevoked && leftRevoked <= right.EffectiveFrom ||
            right.RevokedAt is { } rightRevoked && rightRevoked <= left.EffectiveFrom)
            return false;

        var leftUntil = Earliest(left.EffectiveUntil, left.RevokedAt);
        var rightUntil = Earliest(right.EffectiveUntil, right.RevokedAt);
        var startsAt = left.EffectiveFrom > right.EffectiveFrom
            ? left.EffectiveFrom : right.EffectiveFrom;
        var endsAt = Earliest(leftUntil, rightUntil);
        return endsAt is null || startsAt < endsAt;
    }

    static DateTimeOffset? Earliest(DateTimeOffset? first, DateTimeOffset? second) =>
        first is null ? second : second is null ? first : first < second ? first : second;

    static bool TryClassify(ResponsibilityType existing, ResponsibilityType proposed,
        out ResponsibilityConflictKind kind, out string action)
    {
        ResponsibilityType? work = IsWorkResponsibility(existing) ? existing :
            IsWorkResponsibility(proposed) ? proposed : null;
        var decision = work == existing ? proposed : existing;
        if (work is not null && IsReviewResponsibility(decision))
        {
            kind = ResponsibilityConflictKind.SelfReview;
            action = SeparationOfDutiesActions.Review;
            return true;
        }
        if (work is not null && decision == ResponsibilityType.PolicyApprover)
        {
            kind = ResponsibilityConflictKind.SelfApproval;
            action = SeparationOfDutiesActions.Approve;
            return true;
        }

        kind = default;
        action = string.Empty;
        return false;
    }

    static bool IsWorkResponsibility(ResponsibilityType type) => type is
        ResponsibilityType.ControlOwner or ResponsibilityType.EvidenceContributor or
        ResponsibilityType.CorrectiveActionOwner;

    static bool IsReviewResponsibility(ResponsibilityType type) => type is
        ResponsibilityType.AssignedReviewer or ResponsibilityType.AccessReviewer;
}
