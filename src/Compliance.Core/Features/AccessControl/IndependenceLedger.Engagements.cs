using System.Collections.ObjectModel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    readonly Dictionary<Uuid, ServiceEngagementView> _engagements = [];
    readonly Dictionary<Uuid, List<ServiceEngagementView>> _engagementHistory = [];
    readonly List<EngagementAssignment> _assignmentHistory = [];

    void InitializeEngagements() => On<ServiceEngagementMutationRecorded>(Apply);

    public ServiceEngagementView? Engagement(Uuid engagementId) => _engagements.GetValueOrDefault(engagementId);
    public IReadOnlyList<ServiceEngagementView> Engagements => Array.AsReadOnly(_engagements.Values
        .OrderBy(engagement => engagement.EngagementId.ToString(), StringComparer.Ordinal).ToArray());
    public IReadOnlyList<ServiceEngagementView> EngagementHistory(Uuid engagementId) =>
        Array.AsReadOnly(_engagementHistory.GetValueOrDefault(engagementId)?.ToArray() ?? []);

    public Result<ServiceEngagementView> CreateEngagement(Uuid requestId, Uuid engagementId,
        long expectedSequence, ServiceEngagementDraftContent content, FirmStaffMemberView lead,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || !ValidEngagementContent(content) ||
            !ValidStaff(lead, content.Practice) || lead.StaffMemberId != content.EngagementLeadStaffMemberId)
            return InvalidEngagement();
        var request = new CreateServiceEngagement(_tenantId, engagementId, expectedSequence, content);
        if (Retry<ServiceEngagementView>(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _engagements.ContainsKey(engagementId) || _engagements.Count >= 100)
            return Failure<ServiceEngagementView>(RequestErrorKind.Conflict, "Reload the client sequence; engagement identities are immutable and bounded.");
        if (AssignmentFailure(engagementId, lead) is { } refusal)
            return refusal;
        var view = new ServiceEngagementView(_tenantId, engagementId, 1, content, "draft",
            Array.AsReadOnly(new[] { AssignedStaff(lead, actor, recordedAt) }), actor, recordedAt, false);
        return CommitEngagement(requestId, expectedSequence, request, view);
    }

    public Result<ServiceEngagementView> AmendEngagement(Uuid requestId, Uuid engagementId,
        long expectedSequence, ServiceEngagementDraftContent content, FirmStaffMemberView lead,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || !ValidEngagementContent(content) ||
            !ValidStaff(lead, content.Practice) || lead.StaffMemberId != content.EngagementLeadStaffMemberId)
            return InvalidEngagement();
        var request = new AmendServiceEngagement(_tenantId, engagementId, expectedSequence, content);
        if (Retry<ServiceEngagementView>(requestId, request, actor) is { } retry)
            return retry;
        if (MutableEngagement(engagementId, expectedSequence) is not { } current)
            return UnavailableEngagement();
        if (content.Practice != current.Content.Practice)
            return Failure<ServiceEngagementView>(RequestErrorKind.Conflict, "An engagement amendment cannot rewrite its practice identity.");
        if (AssignmentFailure(engagementId, lead) is { } refusal)
            return refusal;
        var staff = ReplaceStaff(current.Staff, lead, actor, recordedAt);
        if (staff.Count > 100)
            return Failure<ServiceEngagementView>(RequestErrorKind.Conflict, "Engagement staff history is bounded and cannot be truncated.");
        return CommitEngagement(requestId, expectedSequence, request, current with
        {
            Revision = current.Revision + 1,
            Content = content,
            Staff = staff,
            Actor = actor,
            RecordedAt = recordedAt
        });
    }

    public Result<ServiceEngagementView> ProposeEngagementStaff(Uuid requestId, Uuid engagementId,
        long expectedSequence, FirmStaffMemberView staff, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || staff is null || staff.StaffMemberId == Uuid.Empty)
            return InvalidEngagement();
        var request = new ProposeServiceEngagementStaff(_tenantId, engagementId, staff.StaffMemberId, expectedSequence);
        if (Retry<ServiceEngagementView>(requestId, request, actor) is { } retry)
            return retry;
        if (MutableEngagement(engagementId, expectedSequence) is not { } current)
            return UnavailableEngagement();
        if (!ValidStaff(staff, current.Content.Practice))
            return InvalidEngagement();
        if (AssignmentFailure(engagementId, staff) is { } refusal)
            return refusal;
        var assigned = ReplaceStaff(current.Staff, staff, actor, recordedAt);
        if (assigned.Count > 100)
            return Failure<ServiceEngagementView>(RequestErrorKind.Conflict, "Engagement staff history is bounded and cannot be truncated.");
        return CommitEngagement(requestId, expectedSequence, request, current with
        {
            Revision = current.Revision + 1,
            Staff = assigned,
            Actor = actor,
            RecordedAt = recordedAt
        });
    }

    public Result<ServiceEngagementView> WithdrawEngagementStaffProposal(Uuid requestId, Uuid engagementId,
        Uuid staffMemberId, long expectedSequence, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || staffMemberId == Uuid.Empty || !BoundedEngagement(reason))
            return InvalidEngagement();
        var request = new WithdrawServiceEngagementStaffProposal(_tenantId, engagementId, staffMemberId, expectedSequence, reason);
        if (Retry<ServiceEngagementView>(requestId, request, actor) is { } retry)
            return retry;
        if (MutableEngagement(engagementId, expectedSequence) is not { } current)
            return UnavailableEngagement();
        if (current.Content.EngagementLeadStaffMemberId == staffMemberId)
            return Failure<ServiceEngagementView>(RequestErrorKind.Conflict, "Amend the lead before withdrawing their proposal.");
        if (!current.Staff.Any(staff => staff.StaffMemberId == staffMemberId && staff.IsCurrent))
            return Failure<ServiceEngagementView>(RequestErrorKind.NotFound, "The current tentative staff proposal was not found.");
        var remaining = current.Staff.Select(staff => staff.StaffMemberId == staffMemberId
            ? staff with { ProposalState = "withdrawn", IsCurrent = false, Actor = actor, RecordedAt = recordedAt } : staff).ToArray();
        return CommitEngagement(requestId, expectedSequence, request, current with
        {
            Revision = current.Revision + 1,
            Staff = Array.AsReadOnly(remaining),
            Actor = actor,
            RecordedAt = recordedAt
        });
    }

    public Result<ServiceEngagementView> CloseEngagement(Uuid requestId, Uuid engagementId,
        long expectedSequence, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidMutation(requestId, engagementId, actor, recordedAt) || !BoundedEngagement(reason))
            return InvalidEngagement();
        var request = new CloseServiceEngagement(_tenantId, engagementId, expectedSequence, reason);
        if (Retry<ServiceEngagementView>(requestId, request, actor) is { } retry)
            return retry;
        if (Engagement(engagementId) is { Status: "accepted" })
            return CloseActualEngagement(requestId, engagementId, expectedSequence, reason, actor, recordedAt);
        if (MutableEngagement(engagementId, expectedSequence) is not { } current)
            return UnavailableEngagement();
        return CommitEngagement(requestId, expectedSequence, request, current with
        {
            Revision = current.Revision + 1,
            Status = "closed",
            Staff = Array.AsReadOnly(current.Staff.Select(staff => staff with { ProposalState = "withdrawn", IsCurrent = false }).ToArray()),
            Actor = actor,
            RecordedAt = recordedAt
        });
    }

    Result<ServiceEngagementView> CommitEngagement<T>(Uuid requestId, long expectedSequence,
        T request, ServiceEngagementView view)
    {
        var ev = new ServiceEngagementMutationRecorded(_tenantId, requestId, expectedSequence, Intent(request), view);
        if (!Fits(ev))
            return Failure<ServiceEngagementView>(RequestErrorKind.Validation, "The complete engagement event exceeds the bounded payload.");
        RaiseEvent(ev);
        return Result<ServiceEngagementView>.Success(view);
    }

    void Apply(ServiceEngagementMutationRecorded change)
    {
        Fence(change.TenantId, change.ExpectedSequence);
        var current = Engagement(change.Engagement.EngagementId);
        if (change.Engagement.EngagementId == Uuid.Empty || current is null && change.Engagement.Status != "draft" ||
            !ValidEngagementContent(change.Engagement.Content) ||
            !IndependenceRecordValidation.ValidAttribution(change.RequestId, change.Engagement.Actor,
                change.Engagement.RecordedAt, "member") ||
            current is { Status: not "draft" } || current is not null && current.Content.Practice != change.Engagement.Content.Practice ||
            change.Engagement.Staff.Count > 100 ||
            change.Engagement.Status == "draft" && !change.Engagement.Staff.Any(staff =>
                staff.StaffMemberId == change.Engagement.Content.EngagementLeadStaffMemberId && staff.IsCurrent) ||
            change.Engagement.Status == "closed" && change.Engagement.Staff.Any(staff => staff.IsCurrent) ||
            change.Engagement.Staff.Any(staff => staff.StaffMemberId == Uuid.Empty || staff.UserId == Uuid.Empty ||
                staff.DirectoryStaffRevision <= 0 || staff.Practice != change.Engagement.Content.Practice ||
                !IndependenceRecordValidation.ValidAttribution(change.RequestId, staff.Actor, staff.RecordedAt, "member") ||
                staff.IsCurrent != (staff.ProposalState == "proposed")) ||
            change.Engagement.TenantId != _tenantId || change.Engagement.Revision != (current?.Revision ?? 0) + 1 ||
            change.Engagement.ProfessionalAccessGranted || change.Engagement.Status is not ("draft" or "closed") ||
            change.Engagement.Staff.Any(staff => staff.ProfessionalAccessGranted ||
                staff.ProposalState is not ("proposed" or "withdrawn")) ||
            change.Engagement.Staff.Select(staff => staff.StaffMemberId).Distinct().Count() != change.Engagement.Staff.Count)
            throw new InvalidOperationException("Engagement drafts must preserve their owning tenant, revision and lack of professional grants.");
        var view = change.Engagement with { Staff = Array.AsReadOnly(change.Engagement.Staff.ToArray()) };
        _engagements[view.EngagementId] = view;
        if (!_engagementHistory.TryGetValue(view.EngagementId, out var revisions))
            _engagementHistory.Add(view.EngagementId, revisions = []);
        revisions.Add(view);
        _decisions.Add(change.RequestId, (change.Intent, view.Actor, view));
    }

    Result<ServiceEngagementView>? AssignmentFailure(Uuid engagementId, FirmStaffMemberView staff)
    {
        var decision = IndependenceCompartments.CanAssign(_assignmentHistory,
            new EngagementAssignment(_tenantId, engagementId, staff.StaffMemberId, staff.UserId, Practice(staff.Practice)));
        return decision.Code switch
        {
            IndependenceDecisionCode.Allowed => null,
            IndependenceDecisionCode.PersonPracticeConflict => Failure<ServiceEngagementView>(RequestErrorKind.Conflict,
                $"Rule {decision.RuleId} refuses the opposite practice. Conflicting historical engagement: {decision.ConflictingAssignment!.EngagementId}; staff: {decision.ConflictingAssignment.FirmStaffMemberId}; user: {decision.ConflictingAssignment.UserId}."),
            _ => InvalidEngagement(),
        };
    }

    ServiceEngagementView? MutableEngagement(Uuid engagementId, long expectedSequence) =>
        expectedSequence == Sequence && Engagement(engagementId) is { Status: "draft" } current &&
        _engagementHistory[engagementId].Count < 100 ? current : null;

    bool ValidMutation(Uuid requestId, Uuid engagementId, ActorReference actor, DateTimeOffset recordedAt) =>
        _tenantId != Uuid.Empty && engagementId != Uuid.Empty &&
        IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "member");
    static bool ValidEngagementContent(ServiceEngagementDraftContent content) => content is not null &&
        content.Practice is "advisory" or "attest" && BoundedEngagement(content.Scope) &&
        content.PeriodStart != DateOnly.MinValue && (content.PeriodEnd is null || content.PeriodEnd >= content.PeriodStart) &&
        content.EngagementLeadStaffMemberId != Uuid.Empty &&
        (content.ExaminationBoundary is null || content.ExaminationBoundary.BoundaryId != Uuid.Empty &&
            content.ExaminationBoundary.VersionId != Uuid.Empty && content.ExaminationBoundary.Revision > 0);
    static bool ValidStaff(FirmStaffMemberView staff, string practice) => staff is not null &&
        staff.IsActive && staff.StaffMemberId != Uuid.Empty && staff.UserId != Uuid.Empty &&
        staff.Revision > 0 && staff.Practice == practice;
    static bool BoundedEngagement(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2000 &&
        !value.Any(char.IsControl) && value == value.Trim();
    static EngagementPractice Practice(string value) => value switch
    {
        "advisory" => EngagementPractice.Advisory,
        "attest" => EngagementPractice.Attest,
        _ => throw new InvalidOperationException("An engagement practice was invalid."),
    };
    static ServiceEngagementStaffProposalView AssignedStaff(FirmStaffMemberView staff, ActorReference actor,
        DateTimeOffset recordedAt) => new(staff.StaffMemberId, staff.UserId, staff.Practice, staff.Revision,
        "proposed", true, actor, recordedAt, false);
    static ReadOnlyCollection<ServiceEngagementStaffProposalView> ReplaceStaff(IReadOnlyList<ServiceEngagementStaffProposalView> existing,
        FirmStaffMemberView staff, ActorReference actor, DateTimeOffset recordedAt) => Array.AsReadOnly(existing
        .Where(item => item.StaffMemberId != staff.StaffMemberId).Append(AssignedStaff(staff, actor, recordedAt))
        .OrderBy(item => item.StaffMemberId.ToString(), StringComparer.Ordinal).ToArray());
    static Result<ServiceEngagementView> InvalidEngagement() => Failure<ServiceEngagementView>(RequestErrorKind.Validation,
        "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution.");
    static Result<ServiceEngagementView> UnavailableEngagement() => Failure<ServiceEngagementView>(RequestErrorKind.Conflict,
        "Reload the client sequence; only an open draft with bounded retained history can be changed.");
}
