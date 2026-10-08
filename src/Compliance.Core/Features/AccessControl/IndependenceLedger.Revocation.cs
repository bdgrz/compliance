using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    internal Result<ServiceEngagementAcceptanceView> RemoveActualStaff(Uuid requestId, Uuid engagementId,
        Uuid staffMemberId, long expectedSequence, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || staffMemberId == Uuid.Empty || !BoundedEngagement(reason))
            return RefuseAcceptance("Actual-assignment revocation requires an attributed client decision.");
        var intent = "actual-remove:" + Intent(new WithdrawServiceEngagementStaffProposal(_tenantId, engagementId,
            staffMemberId, expectedSequence, reason));
        if (_decisions.TryGetValue(requestId, out var previous))
            return previous.Intent == intent && previous.Actor == actor && previous.Response is ServiceEngagementAcceptanceView response
                ? Result<ServiceEngagementAcceptanceView>.Success(response)
                : RefuseAcceptance("The request identity already records a different revocation decision.");
        if (!CanRevoke(engagementId, expectedSequence) || recordedAt < Engagement(engagementId)!.RecordedAt || !Acceptance(engagementId)!.Assignments.Any(staff =>
            staff.StaffMemberId == staffMemberId && staff.IsCurrent))
            return RefuseAcceptance("Reload the current actual assignment and complete client sequence before revoking.");
        RaiseEvent(new ServiceEngagementAssignmentRevoked(_tenantId, requestId, Sequence, engagementId,
            staffMemberId, reason, intent, actor, recordedAt));
        return Result<ServiceEngagementAcceptanceView>.Success(Acceptance(engagementId)!);
    }

    Result<ServiceEngagementView> CloseActualEngagement(Uuid requestId, Uuid engagementId,
        long expectedSequence, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!CanRevoke(engagementId, expectedSequence) || recordedAt < Engagement(engagementId)!.RecordedAt)
            return UnavailableEngagement();
        var intent = Intent(new CloseServiceEngagement(_tenantId, engagementId, expectedSequence, reason));
        RaiseEvent(new ServiceEngagementAssignmentRevoked(_tenantId, requestId, Sequence, engagementId,
            null, reason, intent, actor, recordedAt));
        return Result<ServiceEngagementView>.Success(Engagement(engagementId)!);
    }

    bool CanRevoke(Uuid engagementId, long expectedSequence) => Sequence == expectedSequence &&
        Acceptance(engagementId) is { Status: "active" } && Engagement(engagementId) is { Status: "accepted" } &&
        _engagementHistory[engagementId].Count < 100;

    void Apply(ServiceEngagementAssignmentRevoked ev)
    {
        if (ev.TenantId != _tenantId || !CanRevoke(ev.EngagementId, ev.ExpectedSequence) ||
            !ValidMutation(ev.RequestId, ev.EngagementId, ev.Actor, ev.RecordedAt) || !BoundedEngagement(ev.Reason) ||
            ev.RecordedAt < Engagement(ev.EngagementId)!.RecordedAt ||
            ev.Intent != (ev.StaffMemberId is { } identity
                ? "actual-remove:" + Intent(new WithdrawServiceEngagementStaffProposal(_tenantId, ev.EngagementId,
                    identity, ev.ExpectedSequence, ev.Reason))
                : Intent(new CloseServiceEngagement(_tenantId, ev.EngagementId, ev.ExpectedSequence, ev.Reason))) ||
            ev.StaffMemberId == Uuid.Empty ||
            ev.StaffMemberId is { } staffId && !Acceptance(ev.EngagementId)!.Assignments.Any(staff => staff.IsCurrent && staff.StaffMemberId == staffId))
            throw new InvalidOperationException("Actual assignment revocation must preserve its client, current accepted assignment and attributed decision.");
        var acceptance = Acceptance(ev.EngagementId)!;
        var engagement = Engagement(ev.EngagementId)!;
        var closes = ev.StaffMemberId is null || ev.StaffMemberId == engagement.Content.EngagementLeadStaffMemberId;
        var revised = acceptance with
        {
            Revision = acceptance.Revision + 1,
            Status = closes ? "closed" : "active",
            ChangedBy = ev.Actor,
            ChangedAt = ev.RecordedAt,
            ChangeReason = ev.Reason,
            Assignments = Array.AsReadOnly(acceptance.Assignments.Select(staff => closes || staff.StaffMemberId == ev.StaffMemberId
                ? staff with { IsCurrent = false } : staff).ToArray())
        };
        Fence(ev.TenantId, ev.ExpectedSequence);
        _acceptances[ev.EngagementId] = revised;
        _acceptanceHistory[ev.EngagementId].Add(revised);
        var view = engagement with
        {
            Revision = engagement.Revision + 1,
            Status = closes ? "closed" : "accepted",
            Actor = ev.Actor,
            RecordedAt = ev.RecordedAt
        };
        _engagements[ev.EngagementId] = view;
        _engagementHistory[ev.EngagementId].Add(view);
        _decisions.Add(ev.RequestId, (ev.Intent, ev.Actor, ev.StaffMemberId is null ? view : revised));
    }
}
