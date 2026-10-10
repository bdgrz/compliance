using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Builds acceptance evidence only from current client, firm-duty, ratification and boundary sources.</summary>
sealed class EventSourcedServiceEngagementAcceptanceEvidenceReader(IAggregateReader reader,
    ProfessionalDutyAuthorityReader duties, CurrentRatifiedIndependenceRulesReader rulesReader,
    IBoundaryDirectoryReader boundaries, BoundaryHistoryReadConsistency boundaryConsistency,
    TimeProvider clock) : IServiceEngagementAcceptanceEvidenceReader
{
    public async ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentAsync(Uuid tenantId,
        Uuid engagementId, Uuid actorUserId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty || actorUserId == Uuid.Empty)
            return Refuse(RequestErrorKind.Validation, "An exact client engagement and personal partner identity are required.");

        var duty = await duties.ReadCurrentAsync(actorUserId, FirmProfessionalDuty.EngagementPartner,
            tenantId, ct).ConfigureAwait(false);
        if (duty is null)
            return Refuse(RequestErrorKind.Forbidden, "The actor has no current partner designation for this client.");
        var rules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        if (rules is null)
            return Refuse(RequestErrorKind.Conflict, "Current ratified independence rules are unavailable.", true);

        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        if (ledger.Engagement(engagementId) is not { Status: "draft" } engagement)
            return Refuse(RequestErrorKind.Conflict, "The exact service engagement draft is unavailable.", true);
        if (ledger.EngagementHistory(engagementId).Any(item => item.RecordedAt > clock.GetUtcNow()) ||
            ledger.History().Services.Any(item => item.RecordedAt > clock.GetUtcNow()))
            return Refuse(RequestErrorKind.Conflict, "A current source has an invalid future timestamp.", true);

        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var partner = duty.Staff;
        if (!MatchesCurrentStaff(partner, partner.StaffMemberId, partner.UserId, partner.Revision,
                engagement.Content.Practice, directory.View().Staff))
            return Refuse(RequestErrorKind.Conflict,
                "The designated partner no longer matches one current active firm-staff directory row.", true);

        var proposed = engagement.Staff.Where(item => item.IsCurrent).ToArray();
        if (proposed.Length == 0)
            return Refuse(RequestErrorKind.Conflict, "The engagement has no current proposed professional team.", true);
        var currentStaff = new List<FirmStaffMemberView>(proposed.Length);
        foreach (var proposal in proposed)
        {
            var match = directory.View().Staff.Where(item => item.StaffMemberId == proposal.StaffMemberId ||
                item.UserId == proposal.UserId).ToArray();
            if (match.Length != 1 || !MatchesCurrentStaff(match[0], proposal.StaffMemberId,
                    proposal.UserId, proposal.DirectoryStaffRevision, proposal.Practice,
                    directory.View().Staff))
                return Refuse(RequestErrorKind.Conflict,
                    "Every proposed assignee must match one current active firm-staff directory row.", true);
            currentStaff.Add(match[0]);
        }

        var serviceIds = ledger.History().Services.Select(item => item.ServiceRecordId).ToArray();
        var acknowledgement = ledger.ManagementAcknowledgements(engagementId)
            .Where(item => item.EngagementRevision == engagement.Revision && item.UserId != partner.UserId &&
                item.CompleteServiceRecordIds.Count == serviceIds.Length &&
                item.CompleteServiceRecordIds.ToHashSet().SetEquals(serviceIds))
            .OrderByDescending(item => item.RecordedAt).FirstOrDefault();
        if (acknowledgement is null)
            return Refuse(RequestErrorKind.Conflict,
                "A separate current client management acknowledgement for the complete service history is required.", true);

        var serviceHistory = ledger.History().Services;
        PartnerIndependenceEvaluationView? evaluation = null;
        if (engagement.Content.Practice == "attest")
        {
            var policy = RuleSet(rules);
            var sourceRecords = serviceHistory.Select(item => new NonattestServiceRecord(tenantId,
                item.Content.ServiceEngagementId, item.Content.ServiceType, item.Content.StartedOn,
                item.Content.EndedOn, item.Content.FirmStaffMemberIds,
                item.Content.InvolvedManagementFunctions)).ToArray();
            var decision = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, policy,
                sourceRecords, engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
            if (decision.Code == IndependenceDecisionCode.PartnerEvaluationRequired)
            {
                evaluation = ledger.PartnerEvaluationsFor(engagementId).LastOrDefault(item =>
                    MatchesEvaluation(item, tenantId, engagement, rules, serviceHistory, partner,
                        duty.Designation, clock.GetUtcNow()));
                if (evaluation is null)
                    return Refuse(RequestErrorKind.Conflict,
                        "The current conditional service history requires a recorded designated-partner evaluation.", true);
            }
            else if (decision.Code != IndependenceDecisionCode.Allowed)
                return Refuse(RequestErrorKind.Conflict,
                    "Impairing, unclassified or unreadable service history cannot be overridden by partner evaluation.", true);
        }

        var boundaryResult = await ReadSelectedBoundaryAsync(tenantId, engagement, ct).ConfigureAwait(false);
        if (!boundaryResult.IsSuccess)
            return Result<ServiceEngagementAcceptanceEvidence>.Failure(boundaryResult.Error!);

        var now = clock.GetUtcNow();
        if (acknowledgement.RecordedAt > now || rules.RecordedAt > now || partner.RecordedAt > now ||
            currentStaff.Any(item => item.RecordedAt > now) || evaluation is { RecordedAt: var evaluatedAt } && evaluatedAt > now)
            return Refuse(RequestErrorKind.Conflict, "Acceptance evidence contains a future-dated source.", true);

        var proof = new VerifiedEngagementAcceptance(tenantId, engagementId, engagement.Revision,
            Uuid.CreateVersion5(tenantId, $"partner-review-v1:{engagementId}:{engagement.Revision}:" +
                $"{rules.Version}:{IndependenceSourceDigest.Services(serviceHistory)}:{partner.UserId}"),
            partner.StaffMemberId, partner.UserId, duty.Designation.SourceReference,
            evaluation?.EvaluationId.ToString(), acknowledgement.AcknowledgementId,
            boundaryResult.Value?.Version, boundaryResult.Value?.Approval,
            Array.AsReadOnly(currentStaff.ToArray()), partner, duty.Designation.Revision, now, evaluation);

        // Source streams are independent of the client ledger. Re-read the authority, rules, and client
        // ledger after assembly; acceptance then uses the ledger's expected-sequence OCC before appending.
        var currentDuty = await duties.ReadCurrentAsync(actorUserId, FirmProfessionalDuty.EngagementPartner,
            tenantId, ct).ConfigureAwait(false);
        var currentRules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        var currentLedger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        var currentBoundary = await ReadSelectedBoundaryAsync(tenantId, engagement, ct).ConfigureAwait(false);
        if (currentDuty != duty || currentRules is null || !SameRules(currentRules, rules) ||
            currentLedger.Sequence != ledger.Sequence ||
            currentLedger.Engagement(engagementId)?.Revision != engagement.Revision ||
            !currentBoundary.IsSuccess || !SameBoundary(boundaryResult.Value, currentBoundary.Value))
            return Refuse(RequestErrorKind.Conflict,
                "Partner authority, rules, client facts or selected boundary changed during evidence assembly; reload and retry.", true);

        return Result<ServiceEngagementAcceptanceEvidence>.Success(new ServiceEngagementAcceptanceEvidence(proof, rules));
    }

    async ValueTask<Result<ResolvedBoundary?>> ReadSelectedBoundaryAsync(Uuid tenantId,
        ServiceEngagementView engagement, CancellationToken ct)
    {
        if (engagement.Content.ExaminationBoundary is not { } selected)
            return Result<ResolvedBoundary?>.Success(null);
        var source = await reader.HydrateAsync(new SystemBoundary(tenantId, selected.BoundaryId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || !source.IsVisible || !source.IsVersionApproved(selected.VersionId))
            return RefuseBoundary("The selected boundary version is not a currently retained approved version.");
        var freshness = await boundaryConsistency.EnsureAsync(tenantId, selected.BoundaryId,
            source.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ResolvedBoundary?>.Failure(freshness.Error!);
        var directory = await boundaries.GetAsync(tenantId, selected.BoundaryId, ct).ConfigureAwait(false);
        var version = await boundaries.GetVersionAsync(tenantId, selected.BoundaryId, selected.VersionId, ct)
            .ConfigureAwait(false);
        if (directory is null || directory.TenantId != tenantId || directory.BoundaryId != selected.BoundaryId ||
            version is not { Status: "approved" } || version.TenantId != tenantId ||
            version.BoundaryId != selected.BoundaryId || version.VersionId != selected.VersionId ||
            version.Revision != selected.Revision)
            return RefuseBoundary("The selected boundary version is missing, stale, foreign or not approved.");
        var decisionPage = await boundaries.ListDecisionsAsync(tenantId, selected.BoundaryId, 200, null, ct)
            .ConfigureAwait(false);
        if (decisionPage is null || decisionPage.NextCursor is not null ||
            decisionPage.Items.Any(item => item.TenantId != tenantId || item.BoundaryId != selected.BoundaryId))
            return RefuseBoundary("The complete selected-boundary decision history is unavailable or over its read limit.");
        var approvals = decisionPage.Items.Where(item => item.Outcome == "approve" &&
            item.VersionId == selected.VersionId && item.Revision == selected.Revision).ToArray();
        if (approvals is not [{ } approval] || approval.DecisionId == Uuid.Empty ||
            approval.DecidedAt == default || approval.DecidedAt > clock.GetUtcNow())
            return RefuseBoundary("The selected boundary does not have one current exact approval decision.");
        return Result<ResolvedBoundary?>.Success(new ResolvedBoundary(version, approval));
    }

    static bool MatchesCurrentStaff(FirmStaffMemberView staff, Uuid staffMemberId, Uuid userId,
        long revision, string practice, IReadOnlyList<FirmStaffMemberView> directory) =>
        staff.IsActive && staff.StaffMemberId == staffMemberId && staff.UserId == userId &&
        staff.Revision == revision && staff.Practice == practice &&
        directory.Count(item => item.StaffMemberId == staffMemberId || item.UserId == userId) == 1;

    static bool MatchesEvaluation(PartnerIndependenceEvaluationView evaluation, Uuid tenantId,
        ServiceEngagementView engagement, IndependenceRuleVersionView rules,
        IReadOnlyList<NonattestServiceView> services, FirmStaffMemberView partner,
        FirmProfessionalDutyDesignationView duty, DateTimeOffset now) =>
        evaluation.TenantId == tenantId && evaluation.EngagementId == engagement.EngagementId &&
        evaluation.DraftRevision == engagement.Revision && evaluation.RuleVersion == rules.Version &&
        evaluation.RuleContentDigest == IndependenceSourceDigest.RuleContent(rules.Content) &&
        evaluation.ServiceHistoryDigest == IndependenceSourceDigest.Services(services) &&
        evaluation.CompleteServiceHistory.Select(item => item.ServiceRecordId)
            .SequenceEqual(services.Select(item => item.ServiceRecordId)) &&
        evaluation.PartnerStaffMemberId == partner.StaffMemberId && evaluation.PartnerUserId == partner.UserId &&
        evaluation.PartnerDirectoryStaffRevision == partner.Revision &&
        evaluation.DutyDesignationId == duty.DesignationId && evaluation.PartnerDutyRevision == duty.Revision &&
        evaluation.DutySourceReference == duty.SourceReference && evaluation.RecordedAt <= now &&
        evaluation.DecisionCode == "partner_evaluation_required" &&
        evaluation.Outcome == "conditionally_compatible" && evaluation.Actor.Kind == "firm_staff" &&
        evaluation.Actor.Id == partner.UserId.ToString();

    static bool SameRules(IndependenceRuleVersionView current, IndependenceRuleVersionView captured) =>
        current.Version == captured.Version && current.IsRatified && captured.IsRatified &&
        IndependenceSourceDigest.RuleContent(current.Content) == IndependenceSourceDigest.RuleContent(captured.Content) &&
        current.Ratification?.RatificationId == captured.Ratification?.RatificationId &&
        current.Ratification?.RuleContentDigest == captured.Ratification?.RuleContentDigest;

    static bool SameBoundary(ResolvedBoundary? first, ResolvedBoundary? second) =>
        first is null && second is null || first is not null && second is not null &&
        first.Version.TenantId == second.Version.TenantId && first.Version.BoundaryId == second.Version.BoundaryId &&
        first.Version.VersionId == second.Version.VersionId && first.Version.Revision == second.Version.Revision &&
        first.Approval.DecisionId == second.Approval.DecisionId && first.Approval.Revision == second.Approval.Revision;

    static IndependenceRuleSet RuleSet(IndependenceRuleVersionView rules) => new(rules.Version,
        rules.Content.LookBackMonths, rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(
            rule.ServiceType, Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());

    static IndependenceServiceClassification Classification(string value) => value switch
    {
        "compatible" => IndependenceServiceClassification.Compatible,
        "conditionally_compatible" => IndependenceServiceClassification.ConditionallyCompatible,
        "impairing" => IndependenceServiceClassification.Impairing,
        _ => throw new InvalidOperationException("An active ratified rule contained an invalid classification.")
    };

    static Result<ResolvedBoundary?> RefuseBoundary(string message) =>
        Result<ResolvedBoundary?>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    static Result<ServiceEngagementAcceptanceEvidence> Refuse(RequestErrorKind kind, string message,
        bool transient = false) => Result<ServiceEngagementAcceptanceEvidence>.Failure(
        new RequestError(kind, message, transient));

    sealed record ResolvedBoundary(BoundaryVersionView Version, BoundaryDecisionView Approval);
}
