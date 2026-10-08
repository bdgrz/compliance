using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    const int MaximumAssignmentReevaluations = 1000;
    readonly List<AssignmentIndependenceReevaluationView> _assignmentReevaluations = [];
    readonly Dictionary<Uuid, (ServiceEngagementAssignmentRevoked Cause,
        ServiceEngagementAcceptanceView Previous, ServiceEngagementAcceptanceView Resulting)> _revocationSources = [];

    Result<AssignmentIndependenceReevaluated> PrepareAssignmentReevaluation(ServiceEngagementAssignmentRevoked cause)
    {
        if (_assignmentReevaluations.Count >= MaximumAssignmentReevaluations ||
            !ValidAssignmentReceiptChronology(cause))
            return Failure<AssignmentIndependenceReevaluated>(RequestErrorKind.Conflict,
                "The complete assignment receipt is bounded and cannot precede retained client sources.");
        var previous = Acceptance(cause.EngagementId)!;
        var resulting = RevisedAcceptance(cause, previous, Engagement(cause.EngagementId)!);
        var receipt = AssignmentReevaluation(cause, previous, resulting);
        var ev = new AssignmentIndependenceReevaluated(_tenantId, cause.ExpectedSequence + 1, receipt);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cause, ComplianceCoreJsonContext.Default.ServiceEngagementAssignmentRevoked).Length +
            JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.AssignmentIndependenceReevaluated).Length;
        if (bytes > MaximumSourceTransactionBytes)
            return Failure<AssignmentIndependenceReevaluated>(RequestErrorKind.Validation,
                "The complete assignment cause and receipt exceed their payload bound; no lifecycle change was recorded.");
        return Result<AssignmentIndependenceReevaluated>.Success(ev);
    }

    bool ValidAssignmentReceiptChronology(ServiceEngagementAssignmentRevoked cause) =>
        _acceptanceHistory.Values.SelectMany(history => history).All(acceptance =>
            (acceptance.ChangedAt ?? acceptance.RecordedAt) <= cause.RecordedAt) &&
        _services.All(service => service.RecordedAt <= cause.RecordedAt);

    static ServiceEngagementAcceptanceView RevisedAcceptance(ServiceEngagementAssignmentRevoked cause,
        ServiceEngagementAcceptanceView acceptance, ServiceEngagementView engagement)
    {
        var closes = cause.StaffMemberId is null || cause.StaffMemberId == engagement.Content.EngagementLeadStaffMemberId;
        return acceptance with
        {
            Revision = acceptance.Revision + 1,
            Status = closes ? "closed" : "active",
            ChangedBy = cause.Actor,
            ChangedAt = cause.RecordedAt,
            ChangeReason = cause.Reason,
            Assignments = Array.AsReadOnly(acceptance.Assignments.Select(staff => closes || staff.StaffMemberId == cause.StaffMemberId
                ? staff with { IsCurrent = false } : staff).ToArray())
        };
    }

    AssignmentIndependenceReevaluationView AssignmentReevaluation(ServiceEngagementAssignmentRevoked cause,
        ServiceEngagementAcceptanceView previous, ServiceEngagementAcceptanceView resulting)
    {
        var beforeHash = AcceptanceDigest(previous);
        var afterHash = AcceptanceDigest(resulting);
        return new AssignmentIndependenceReevaluationView(
            Uuid.CreateVersion5(cause.RequestId, $"assignment_reevaluation_v1:{cause.EngagementId}:{previous.Revision}:{beforeHash}:{afterHash}"),
            _tenantId, cause.RequestId, cause.ExpectedSequence + 1, cause.EngagementId, cause.StaffMemberId,
            cause.StaffMemberId is null ? "engagement_closed" : "assignment_removed", cause.Reason, cause.Intent,
            FreezeAcceptedSnapshot(previous), beforeHash, FreezeAcceptedSnapshot(resulting), afterHash,
            Array.AsReadOnly(_assignmentHistory.Select(assignment => new HistoricalEngagementAssignmentView(
                assignment.ClientTenantId, assignment.EngagementId, assignment.FirmStaffMemberId, assignment.UserId,
                assignment.Practice == EngagementPractice.Attest ? "attest" : "advisory")).ToArray()),
            "review_required", true, cause.Actor, cause.RecordedAt);
    }

    static ServiceEngagementAcceptanceView FreezeAcceptedSnapshot(ServiceEngagementAcceptanceView acceptance) => acceptance with
    {
        Rules = IndependenceRecordValidation.Freeze(acceptance.Rules),
        CompleteServiceHistory = Array.AsReadOnly(acceptance.CompleteServiceHistory.Select(IndependenceRecordValidation.Freeze).ToArray()),
        Assignments = Array.AsReadOnly(acceptance.Assignments.ToArray()),
        ConsideredServiceRecordIds = Array.AsReadOnly(acceptance.ConsideredServiceRecordIds.ToArray())
    };

    static AssignmentIndependenceReevaluationView FreezeAssignmentReevaluation(AssignmentIndependenceReevaluationView receipt) => receipt with
    {
        PreviousAcceptance = FreezeAcceptedSnapshot(receipt.PreviousAcceptance),
        ResultingAcceptance = FreezeAcceptedSnapshot(receipt.ResultingAcceptance),
        CanonicalAssignmentHistory = Array.AsReadOnly(receipt.CanonicalAssignmentHistory.ToArray())
    };

    void Apply(AssignmentIndependenceReevaluated ev)
    {
        var receipt = ev.Reevaluation;
        if (receipt is null || ev.TenantId != _tenantId || ev.ExpectedSequence != Sequence ||
            _assignmentReevaluations.Count >= MaximumAssignmentReevaluations ||
            _assignmentReevaluations.Any(item => item.ReevaluationId == receipt.ReevaluationId) ||
            !_revocationSources.TryGetValue(receipt.CausalRequestId, out var source) ||
            Sequence != source.Cause.ExpectedSequence + 1 || !ValidAssignmentReceiptChronology(source.Cause) ||
            Intent(Acceptance(source.Cause.EngagementId)) != Intent(source.Resulting) ||
            Intent(receipt) != Intent(AssignmentReevaluation(source.Cause, source.Previous, source.Resulting)))
            throw new InvalidOperationException("An assignment receipt must preserve its exact lifecycle cause, snapshots and permanent canonical history.");
        var frozen = FreezeAssignmentReevaluation(receipt);
        Fence(ev.TenantId, ev.ExpectedSequence);
        _assignmentReevaluations.Add(frozen);
    }
}
