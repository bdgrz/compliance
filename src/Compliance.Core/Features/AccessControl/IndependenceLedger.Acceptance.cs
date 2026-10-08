using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    readonly Dictionary<Uuid, ServiceEngagementAcceptanceView> _acceptances = [];
    readonly Dictionary<Uuid, List<ServiceEngagementAcceptanceView>> _acceptanceHistory = [];

    public ServiceEngagementAcceptanceView? Acceptance(Uuid engagementId) => _acceptances.GetValueOrDefault(engagementId);
    public IReadOnlyList<ServiceEngagementAcceptanceView> AcceptanceHistory(Uuid engagementId) =>
        Array.AsReadOnly(_acceptanceHistory.GetValueOrDefault(engagementId)?.ToArray() ?? []);
    public IReadOnlyList<EngagementAssignment> ActualAssignmentHistory => Array.AsReadOnly(_assignmentHistory.ToArray());

    internal Result<ServiceEngagementAcceptanceView> AcceptEngagement(Uuid requestId, long expectedSequence,
        VerifiedEngagementAcceptance proof, IndependenceRuleVersionView rules, DateTimeOffset recordedAt)
    {
        if (proof is null || rules is null || requestId == Uuid.Empty || recordedAt == default ||
            proof.TenantId != _tenantId || proof.EngagementId == Uuid.Empty || proof.ReviewTaskId == Uuid.Empty ||
            proof.PartnerStaffMemberId == Uuid.Empty || proof.PartnerUserId == Uuid.Empty ||
            !ValidPartner(proof) || !ValidProofTimes(proof, rules, recordedAt) || !BoundedEngagement(proof.AuthorityReference) || proof.PartnerEvaluationReference is not null &&
            !BoundedEngagement(proof.PartnerEvaluationReference) || !rules.IsRatified || rules.Version <= 0 ||
            !IndependenceRecordValidation.ValidRules(rules.Content) || !ValidApprovedBoundary(proof))
            return RefuseAcceptance("Verified scoped partner authority, approved boundary and ratified rules are required.");
        var actor = new ActorReference("firm_staff", proof.PartnerUserId.ToString(), "Verified engagement partner");
        var intent = $"{expectedSequence}:{Intent(proof)}:{Intent(rules)}";
        if (_decisions.TryGetValue(requestId, out var previous))
            return previous.Intent == intent && previous.Actor == actor && previous.Response is ServiceEngagementAcceptanceView response
                ? Result<ServiceEngagementAcceptanceView>.Success(response)
                : RefuseAcceptance("The request identity already records different acceptance sources or authority.");
        if (expectedSequence != Sequence || Engagement(proof.EngagementId) is not { Status: "draft" } engagement ||
            engagement.Revision != proof.ReviewedDraftRevision ||
            !MatchingSelectedBoundary(engagement, proof) || _acceptances.ContainsKey(proof.EngagementId) ||
            _engagementHistory[proof.EngagementId].Count >= 100 || _assignmentHistory.Count + engagement.Staff.Count(staff => staff.IsCurrent) > 1000)
            return RefuseAcceptance("Reload the exact draft and complete client sequence before accepting.");
        if (recordedAt < engagement.RecordedAt || _services.Any(service => service.RecordedAt > recordedAt) ||
            !MatchingAcknowledgement(proof.ManagementAcknowledgementId, engagement, proof.PartnerUserId, recordedAt) ||
            _engagementHistory[proof.EngagementId].Any(view => view.Actor.Id == RbacIds.Member(_tenantId, proof.PartnerUserId).ToString()))
            return RefuseAcceptance("Acceptance requires current client management acknowledgement and a separate reviewer.");
        var proposed = engagement.Staff.Where(staff => staff.IsCurrent).ToArray();
        if (proof.CurrentStaff is null || proposed.Length != proof.CurrentStaff.Count || proposed.Any(staff =>
            !proof.CurrentStaff.Any(current => current.StaffMemberId == staff.StaffMemberId &&
                current.UserId == staff.UserId && current.Revision == staff.DirectoryStaffRevision && ValidStaff(current, staff.Practice))))
            return RefuseAcceptance("Every tentative assignee must match the current active directory snapshot.");
        if (!ValidStaff(proof.CurrentPartner, engagement.Content.Practice) || AssignmentFailure(proof.EngagementId, proof.CurrentPartner) is not null)
            return RefuseAcceptance("The approving partner must match the current practice and have no opposite actual assignment history.");
        foreach (var staff in proof.CurrentStaff)
            if (AssignmentFailure(proof.EngagementId, staff) is { } conflict)
                return Result<ServiceEngagementAcceptanceView>.Failure(conflict.Error!);
        var policyResult = AcceptancePolicy(engagement, rules, proof.PartnerEvaluationReference);
        if (policyResult.Decision.Code != IndependenceDecisionCode.Allowed)
            return RefuseAcceptance("Independence policy refuses acceptance; impairment and unclassified services cannot be overridden.");
        var view = new ServiceEngagementAcceptanceView(_tenantId, proof.EngagementId, engagement.Revision, 1,
            proof.ReviewTaskId, proof.PartnerStaffMemberId, proof.PartnerUserId, proof.CurrentPartner.Revision, proof.PartnerDutyRevision, proof.AuthorityReference,
            proof.PartnerEvaluationReference, proof.ManagementAcknowledgementId, proof.Boundary?.BoundaryId,
            proof.Boundary?.VersionId, proof.Boundary?.Revision, proof.BoundaryApproval?.DecisionId,
            new EngagementAcceptanceSourceTimes(proof.CurrentPartner.RecordedAt, proof.PartnerAuthorityVerifiedAt,
                proof.Boundary?.ChangedAt, proof.BoundaryApproval?.DecidedAt), IndependenceRecordValidation.Freeze(rules), Array.AsReadOnly(_services.Select(IndependenceRecordValidation.Freeze).ToArray()),
            "allowed", AcceptanceOutcome(policyResult.Decision), policyResult.ConsideredIds,
            Array.AsReadOnly(proposed.Select(staff => new EngagementActualAssignmentView(staff.StaffMemberId, staff.UserId,
                staff.Practice, staff.DirectoryStaffRevision,
                proof.CurrentStaff.Single(current => current.StaffMemberId == staff.StaffMemberId).RecordedAt, true, actor, recordedAt)).ToArray()), "active", actor, recordedAt);
        var ev = new ServiceEngagementAcceptanceRecorded(_tenantId, requestId, Sequence, intent, view);
        if (!Fits(ev))
            return RefuseAcceptance("The complete accepted snapshot exceeds the bounded event payload; sources were not truncated.");
        RaiseEvent(ev);
        return Result<ServiceEngagementAcceptanceView>.Success(view);
    }

    public bool IsEligibleForProfessionalAccess(Uuid engagementId, Uuid staffMemberId, Uuid userId,
        long currentRuleVersion, long currentDirectoryStaffRevision, DateTimeOffset effectiveAt) =>
        Acceptance(engagementId) is { Status: "active" } acceptance && acceptance.RecordedAt <= effectiveAt &&
        Engagement(engagementId) is { Status: "accepted" } engagement &&
        DateOnly.FromDateTime(effectiveAt.UtcDateTime) >= engagement.Content.PeriodStart &&
        (engagement.Content.PeriodEnd is null || DateOnly.FromDateTime(effectiveAt.UtcDateTime) <= engagement.Content.PeriodEnd) &&
        acceptance.Rules.Version == currentRuleVersion &&
        CompleteFacts(acceptance.CompleteServiceHistory.Select(service => service.ServiceRecordId).ToArray()) &&
        acceptance.Assignments.Any(staff => staff.IsCurrent && staff.StaffMemberId == staffMemberId &&
            staff.UserId == userId && staff.AssignedAt <= effectiveAt && staff.DirectoryStaffRevision == currentDirectoryStaffRevision);

    bool MatchingAcknowledgement(Uuid acknowledgementId, ServiceEngagementView engagement, Uuid partnerUserId, DateTimeOffset acceptedAt) =>
        _managementAcknowledgements.Any(ack => ack.AcknowledgementId == acknowledgementId &&
            ack.EngagementId == engagement.EngagementId && ack.EngagementRevision == engagement.Revision &&
            ack.UserId != partnerUserId && ack.RecordedAt <= acceptedAt && CompleteFacts(ack.CompleteServiceRecordIds));

    (IndependenceDecision Decision, IReadOnlyList<Uuid> ConsideredIds) AcceptancePolicy(
        ServiceEngagementView engagement, IndependenceRuleVersionView rules, string? evaluationReference)
    {
        if (engagement.Content.Practice == "advisory")
            return (IndependenceDecision.Allow, Array.AsReadOnly(Array.Empty<Uuid>()));
        var policy = new IndependenceRuleSet(rules.Version, rules.Content.LookBackMonths,
            rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());
        var sources = _services.Select(service => new NonattestServiceRecord(_tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds, service.Content.InvolvedManagementFunctions)).ToArray();
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(_tenantId, policy, sources,
            engagement.Content.PeriodStart, evaluationReference is not null);
        var considered = decision.ConsideredServices.Select(assessment => assessment.Service).ToHashSet();
        return (decision, Array.AsReadOnly(_services.Where((_, index) => considered.Contains(sources[index]))
            .Select(service => service.ServiceRecordId).ToArray()));
    }

    static string AcceptanceOutcome(IndependenceDecision decision) => decision.EvaluationOutcome switch
    {
        IndependenceEvaluationOutcome.Compatible => "compatible",
        IndependenceEvaluationOutcome.ConditionallyCompatible => "conditionally_compatible",
        _ => "not_applicable"
    };

    static bool ValidProofTimes(VerifiedEngagementAcceptance proof, IndependenceRuleVersionView rules, DateTimeOffset acceptedAt) =>
        proof.PartnerAuthorityVerifiedAt != default && proof.PartnerAuthorityVerifiedAt <= acceptedAt &&
        proof.CurrentPartner.RecordedAt != default && proof.CurrentPartner.RecordedAt <= acceptedAt &&
        rules.RecordedAt != default && rules.RecordedAt <= acceptedAt &&
        (proof.Boundary is null || proof.Boundary.ChangedAt != default && proof.Boundary.ChangedAt <= acceptedAt) &&
        (proof.BoundaryApproval is null || proof.BoundaryApproval.DecidedAt != default && proof.BoundaryApproval.DecidedAt <= acceptedAt) &&
        proof.CurrentStaff is not null && proof.CurrentStaff.All(staff => staff.RecordedAt != default && staff.RecordedAt <= acceptedAt);

    static bool ValidRecordedSourceTimes(ServiceEngagementAcceptanceView acceptance) => acceptance.SourceTimes is { } source &&
        source.PartnerDirectoryRecordedAt != default && source.PartnerDirectoryRecordedAt <= acceptance.RecordedAt &&
        source.PartnerAuthorityVerifiedAt != default && source.PartnerAuthorityVerifiedAt <= acceptance.RecordedAt &&
        acceptance.Rules.RecordedAt != default && acceptance.Rules.RecordedAt <= acceptance.RecordedAt &&
        (acceptance.BoundaryId is null
            ? source.BoundaryChangedAt is null && source.BoundaryApprovedAt is null
            : source.BoundaryChangedAt is { } changed && changed != default && changed <= acceptance.RecordedAt &&
              source.BoundaryApprovedAt is { } approved && approved != default && approved <= acceptance.RecordedAt);

    static bool ValidPartner(VerifiedEngagementAcceptance proof) => proof.PartnerDutyRevision > 0 &&
        proof.CurrentPartner is { IsActive: true } partner && partner.Revision > 0 &&
        partner.StaffMemberId == proof.PartnerStaffMemberId && partner.UserId == proof.PartnerUserId;

    bool ValidApprovedBoundary(VerifiedEngagementAcceptance proof) =>
        proof.Boundary is null && proof.BoundaryApproval is null || proof.Boundary is { Status: "approved" } boundary &&
        proof.BoundaryApproval is { Outcome: "approve" } approval && boundary.TenantId == _tenantId &&
        approval.TenantId == _tenantId && boundary.BoundaryId != Uuid.Empty && boundary.VersionId != Uuid.Empty &&
        boundary.Revision > 0 && approval.DecisionId != Uuid.Empty && approval.BoundaryId == boundary.BoundaryId &&
        approval.VersionId == boundary.VersionId && approval.Revision == boundary.Revision;

    static bool MatchingSelectedBoundary(ServiceEngagementView engagement, VerifiedEngagementAcceptance proof) =>
        engagement.Content.ExaminationBoundary is { } selected
            ? proof.Boundary is { } boundary && proof.BoundaryApproval is not null && selected.BoundaryId == boundary.BoundaryId &&
              selected.VersionId == boundary.VersionId && selected.Revision == boundary.Revision
            : proof.Boundary is null && proof.BoundaryApproval is null;

    static bool MatchingAcceptedBoundary(ServiceEngagementView engagement, ServiceEngagementAcceptanceView acceptance) =>
        engagement.Content.ExaminationBoundary is { } selected
            ? selected.BoundaryId == acceptance.BoundaryId && selected.VersionId == acceptance.BoundaryVersionId &&
              selected.Revision == acceptance.BoundaryRevision && acceptance.BoundaryApprovalDecisionId is { } decisionId && decisionId != Uuid.Empty
            : acceptance.BoundaryId is null && acceptance.BoundaryVersionId is null && acceptance.BoundaryRevision is null &&
              acceptance.BoundaryApprovalDecisionId is null;

    void Apply(ServiceEngagementAcceptanceRecorded ev)
    {
        var acceptance = ev.Acceptance;
        var engagement = Engagement(acceptance.EngagementId);
        if (ev.TenantId != _tenantId || ev.ExpectedSequence != Sequence || acceptance.TenantId != _tenantId ||
            ev.RequestId == Uuid.Empty || _decisions.ContainsKey(ev.RequestId) || string.IsNullOrWhiteSpace(ev.Intent) || engagement is not { Status: "draft" } ||
            acceptance.ReviewedDraftRevision != engagement.Revision || acceptance.Revision != 1 || acceptance.Status != "active" ||
            !acceptance.Rules.IsRatified || !IndependenceRecordValidation.ValidRules(acceptance.Rules.Content) ||
            acceptance.Rules.Version <= 0 || acceptance.ReviewTaskId == Uuid.Empty || acceptance.PartnerStaffMemberId == Uuid.Empty ||
            acceptance.PartnerUserId == Uuid.Empty || acceptance.PartnerDirectoryStaffRevision <= 0 || acceptance.PartnerDutyRevision <= 0 ||
            IndependenceCompartments.CanAssign(_assignmentHistory, new EngagementAssignment(_tenantId, acceptance.EngagementId,
                acceptance.PartnerStaffMemberId, acceptance.PartnerUserId, Practice(engagement.Content.Practice))).Code != IndependenceDecisionCode.Allowed ||
            _engagementHistory[acceptance.EngagementId].Any(view => view.Actor.Id == RbacIds.Member(_tenantId, acceptance.PartnerUserId).ToString()) ||
            acceptance.Actor.Kind != "firm_staff" ||
            acceptance.Actor.Id != acceptance.PartnerUserId.ToString() || !BoundedEngagement(acceptance.AuthorityReference) ||
            acceptance.RecordedAt == default || acceptance.RecordedAt < engagement.RecordedAt ||
            _services.Any(service => service.RecordedAt > acceptance.RecordedAt) || !ValidRecordedSourceTimes(acceptance) ||
            acceptance.ChangedBy is not null || acceptance.ChangedAt is not null ||
            acceptance.ChangeReason is not null || !MatchingAcceptedBoundary(engagement, acceptance) ||
            acceptance.PartnerEvaluationReference is not null && !BoundedEngagement(acceptance.PartnerEvaluationReference) ||
            !MatchingAcknowledgement(acceptance.ManagementAcknowledgementId, engagement, acceptance.PartnerUserId, acceptance.RecordedAt) ||
            !SameServiceSnapshots(acceptance.CompleteServiceHistory) ||
            !MatchingAcceptancePolicy(engagement, acceptance) ||
            !ValidInitialAssignments(engagement, acceptance) || _acceptances.ContainsKey(acceptance.EngagementId) ||
            _engagementHistory[engagement.EngagementId].Count >= 100 ||
            _assignmentHistory.Count + acceptance.Assignments.Count > 1000)
            throw new InvalidOperationException("Accepted engagements require exact immutable client sources, ratified rules and actual independent assignments.");
        Fence(ev.TenantId, ev.ExpectedSequence);
        acceptance = acceptance with
        {
            Rules = IndependenceRecordValidation.Freeze(acceptance.Rules),
            CompleteServiceHistory = Array.AsReadOnly(acceptance.CompleteServiceHistory.Select(IndependenceRecordValidation.Freeze).ToArray()),
            Assignments = Array.AsReadOnly(acceptance.Assignments.ToArray()),
            ConsideredServiceRecordIds = Array.AsReadOnly(acceptance.ConsideredServiceRecordIds.ToArray())
        };
        _acceptances.Add(acceptance.EngagementId, acceptance);
        _acceptanceHistory.Add(acceptance.EngagementId, [acceptance]);
        _assignmentHistory.AddRange(acceptance.Assignments.Select(staff => new EngagementAssignment(_tenantId,
            acceptance.EngagementId, staff.StaffMemberId, staff.UserId, Practice(staff.Practice))));
        var accepted = engagement with
        {
            Revision = engagement.Revision + 1,
            Status = "accepted",
            Actor = acceptance.Actor,
            RecordedAt = acceptance.RecordedAt
        };
        _engagements[accepted.EngagementId] = accepted;
        _engagementHistory[accepted.EngagementId].Add(accepted);
        _decisions.Add(ev.RequestId, (ev.Intent, acceptance.Actor, acceptance));
    }

    bool MatchingAcceptancePolicy(ServiceEngagementView engagement, ServiceEngagementAcceptanceView acceptance)
    {
        var result = AcceptancePolicy(engagement, acceptance.Rules, acceptance.PartnerEvaluationReference);
        return result.Decision.Code == IndependenceDecisionCode.Allowed && acceptance.DecisionCode == "allowed" &&
            acceptance.EvaluationOutcome == AcceptanceOutcome(result.Decision) && acceptance.ConsideredServiceRecordIds is not null &&
            acceptance.ConsideredServiceRecordIds.SequenceEqual(result.ConsideredIds);
    }

    bool SameServiceSnapshots(IReadOnlyList<NonattestServiceView> history) => history is not null &&
        CompleteFacts(history.Select(service => service.ServiceRecordId).ToArray()) && history.All(service =>
            _services.Any(current => current.ServiceRecordId == service.ServiceRecordId && Intent(current) == Intent(service)));

    bool ValidInitialAssignments(ServiceEngagementView engagement, ServiceEngagementAcceptanceView acceptance)
    {
        var proposed = engagement.Staff.Where(staff => staff.IsCurrent).ToArray();
        return acceptance.Assignments is { Count: > 0 and <= 100 } && acceptance.Assignments.Count == proposed.Length &&
            acceptance.Assignments.Select(staff => staff.StaffMemberId).Distinct().Count() == proposed.Length &&
            acceptance.Assignments.All(staff => staff.IsCurrent && staff.DirectoryStaffRecordedAt != default &&
                staff.DirectoryStaffRecordedAt <= acceptance.RecordedAt && staff.Actor == acceptance.Actor && staff.AssignedAt == acceptance.RecordedAt &&
                proposed.Any(proposal => proposal.StaffMemberId == staff.StaffMemberId && proposal.UserId == staff.UserId &&
                    proposal.Practice == staff.Practice && proposal.DirectoryStaffRevision == staff.DirectoryStaffRevision) &&
                IndependenceCompartments.CanAssign(_assignmentHistory, new EngagementAssignment(_tenantId,
                    engagement.EngagementId, staff.StaffMemberId, staff.UserId, Practice(staff.Practice))).Code == IndependenceDecisionCode.Allowed);
    }

    static Result<ServiceEngagementAcceptanceView> RefuseAcceptance(string message) =>
        Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Conflict, message);
}
