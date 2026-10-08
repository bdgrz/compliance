using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    readonly List<EngagementManagementAcknowledgementView> _managementAcknowledgements = [];

    public IReadOnlyList<EngagementManagementAcknowledgementView> ManagementAcknowledgements(Uuid engagementId) =>
        Array.AsReadOnly(_managementAcknowledgements.Where(ack => ack.EngagementId == engagementId).ToArray());

    public Result<EngagementManagementAcknowledgementView> AcknowledgeManagement(Uuid requestId,
        AcknowledgeEngagementManagement request, Uuid userId, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (request.TenantId != _tenantId || request.AcknowledgementId == Uuid.Empty || userId == Uuid.Empty ||
            !ValidMutation(requestId, request.EngagementId, actor, recordedAt) ||
            actor.Id != RbacIds.Member(_tenantId, userId).ToString() || !BoundedEngagement(request.Statement) ||
            request.CompleteServiceRecordIds is null || request.CompleteServiceRecordIds.Count > 100)
            return Failure<EngagementManagementAcknowledgementView>(RequestErrorKind.Validation,
                "Management responsibility must be acknowledged personally by the attributed owning-client member.");
        if (Retry<EngagementManagementAcknowledgementView>(requestId, request, actor) is { } retry)
            return retry;
        if (request.ExpectedSequence != Sequence || Engagement(request.EngagementId) is not { Status: "draft" } engagement ||
            engagement.Revision != request.ExpectedEngagementRevision || !CompleteFacts(request.CompleteServiceRecordIds) ||
            _managementAcknowledgements.Count >= 100 || _managementAcknowledgements.Any(ack => ack.AcknowledgementId == request.AcknowledgementId))
            return Failure<EngagementManagementAcknowledgementView>(RequestErrorKind.Conflict,
                "Reload the complete client facts and engagement revision before acknowledging management responsibility.");
        if (HasAttestHistory(userId))
            return Failure<EngagementManagementAcknowledgementView>(RequestErrorKind.Forbidden,
                "An actual Attest assignee cannot author client management decisions.");
        var acknowledgement = new EngagementManagementAcknowledgementView(_tenantId, request.EngagementId,
            request.AcknowledgementId, engagement.Revision, Array.AsReadOnly(request.CompleteServiceRecordIds.ToArray()),
            request.Statement, userId, actor, recordedAt);
        var ev = new EngagementManagementAcknowledged(_tenantId, requestId, Sequence, acknowledgement);
        if (!Fits(ev))
            return Failure<EngagementManagementAcknowledgementView>(RequestErrorKind.Validation, "The complete acknowledgement exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<EngagementManagementAcknowledgementView>.Success(acknowledgement);
    }

    bool HasAttestHistory(Uuid userId) => _assignmentHistory.Any(assignment => assignment.UserId == userId &&
        assignment.Practice == EngagementPractice.Attest);

    bool CompleteFacts(IReadOnlyList<Uuid> ids) => ids.Distinct().Count() == ids.Count &&
        ids.Count == _services.Count && ids.ToHashSet().SetEquals(_services.Select(service => service.ServiceRecordId));

    void Apply(EngagementManagementAcknowledged ev)
    {
        var ack = ev.Acknowledgement;
        if (ev.TenantId != _tenantId || ev.ExpectedSequence != Sequence || ack.TenantId != _tenantId ||
            ack.AcknowledgementId == Uuid.Empty || ack.UserId == Uuid.Empty ||
            !ValidMutation(ev.RequestId, ack.EngagementId, ack.Actor, ack.RecordedAt) ||
            ack.Actor.Id != RbacIds.Member(_tenantId, ack.UserId).ToString() || !BoundedEngagement(ack.Statement) ||
            ack.CompleteServiceRecordIds is null || !CompleteFacts(ack.CompleteServiceRecordIds) || HasAttestHistory(ack.UserId) ||
            Engagement(ack.EngagementId) is not { Status: "draft" } engagement || engagement.Revision != ack.EngagementRevision ||
            _managementAcknowledgements.Count >= 100 || _managementAcknowledgements.Any(item => item.AcknowledgementId == ack.AcknowledgementId))
            throw new InvalidOperationException("A personal management acknowledgement must preserve its exact client, draft, facts and member attribution.");
        Fence(ev.TenantId, ev.ExpectedSequence);
        ack = ack with { CompleteServiceRecordIds = Array.AsReadOnly(ack.CompleteServiceRecordIds.ToArray()) };
        _managementAcknowledgements.Add(ack);
        Remember(ev.RequestId, new AcknowledgeEngagementManagement(_tenantId, ack.EngagementId, ack.AcknowledgementId,
            ev.ExpectedSequence, ack.EngagementRevision, ack.CompleteServiceRecordIds, ack.Statement), ack.Actor, ack);
    }
}
