using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The M0-D26 strict independence wall between advisory and attest work for one client, as in-code
///     rules that engagement assignment, record authorizers, and engagement acceptance apply.
/// </summary>
public static class IndependenceCompartments
{
    const int MaximumDateOnlyMonthOffset = 120_000;

    /// <summary>One person may never hold both an advisory and an attest assignment for the same client.</summary>
    public static IndependenceDecision CanAssign(IEnumerable<EngagementAssignment> clientAssignments,
        EngagementAssignment candidate)
    {
        ArgumentNullException.ThrowIfNull(clientAssignments);
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.ClientTenantId == Uuid.Empty || candidate.EngagementId == Uuid.Empty ||
            candidate.FirmStaffMemberId == Uuid.Empty || candidate.UserId == Uuid.Empty ||
            !Enum.IsDefined(candidate.Practice))
            return new IndependenceDecision(IndependenceDecisionCode.AssignmentInvalid);

        var assignmentHistory = clientAssignments.ToArray();
        if (assignmentHistory.Any(assignment => assignment is null || assignment.ClientTenantId == Uuid.Empty))
            return new IndependenceDecision(IndependenceDecisionCode.AssignmentInvalid);
        var clientHistory = assignmentHistory.Where(assignment =>
            assignment.ClientTenantId == candidate.ClientTenantId).ToArray();
        if (clientHistory.Any(assignment => assignment.EngagementId == Uuid.Empty ||
                assignment.FirmStaffMemberId == Uuid.Empty || assignment.UserId == Uuid.Empty ||
                !Enum.IsDefined(assignment.Practice)))
            return new IndependenceDecision(IndependenceDecisionCode.AssignmentInvalid);
        var crossesWall = clientHistory.Any(existing =>
            existing.ClientTenantId == candidate.ClientTenantId &&
            existing.FirmStaffMemberId == candidate.FirmStaffMemberId &&
            existing.Practice != candidate.Practice);
        return crossesWall
            ? new IndependenceDecision(IndependenceDecisionCode.PersonPracticeConflict)
            : IndependenceDecision.Allow;
    }

    /// <summary>
    ///     Returns whether the wall blocks the actor from a client's record compartment: attest assignees
    ///     cannot read that client's advisory working notes. This is a deny rule layered on the ordinary
    ///     grant, never a grant: an actor it does not block still needs a membership or advisory
    ///     assignment that permits the read.
    /// </summary>
    public static bool IsBlockedByWall(Uuid clientTenantId, IEnumerable<EngagementAssignment> actorAssignments,
        RecordCompartment compartment)
    {
        ArgumentNullException.ThrowIfNull(actorAssignments);
        return compartment == RecordCompartment.AdvisoryWorkingNotes &&
            actorAssignments.Any(assignment =>
                assignment.ClientTenantId == clientTenantId &&
                assignment.Practice == EngagementPractice.Attest);
    }

    /// <summary>
    ///     An attest engagement cannot be accepted for a client that received control design,
    ///     implementation, or operation from the firm within the rule-set look-back, including
    ///     ongoing work. A conditionally compatible service in the same window requires a recorded
    ///     partner evaluation.
    /// </summary>
    public static IndependenceDecision CanAcceptAttestEngagement(Uuid clientTenantId,
        IndependenceRuleSet ruleSet, IEnumerable<NonattestServiceRecord> serviceHistory,
        DateOnly examinationPeriodStart, bool partnerEvaluationRecorded)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(serviceHistory);
        if (clientTenantId == Uuid.Empty || examinationPeriodStart == DateOnly.MinValue)
            return new IndependenceDecision(IndependenceDecisionCode.EvaluationInputInvalid);
        if (!ruleSet.IsValid)
            return new IndependenceDecision(IndependenceDecisionCode.RuleSetInvalid);
        if (ruleSet.LookBackMonths > MaximumDateOnlyMonthOffset)
            return new IndependenceDecision(IndependenceDecisionCode.RuleSetInvalid);

        var history = serviceHistory.ToArray();
        if (history.Any(service => service is null || service.ClientTenantId == Uuid.Empty))
            return new IndependenceDecision(IndependenceDecisionCode.ServiceHistoryInvalid);
        var clientHistory = history.Where(service => service.ClientTenantId == clientTenantId).ToArray();
        if (clientHistory.Any(service => service.ServiceEngagementId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(service.ServiceType) ||
                service.StartedOn == DateOnly.MinValue ||
                service.EndedOn is { } endedOn && endedOn < service.StartedOn ||
                service.FirmStaffMemberIds is null || service.FirmStaffMemberIds.Count == 0 ||
                service.FirmStaffMemberIds.Contains(Uuid.Empty) ||
                service.FirmStaffMemberIds.Distinct().Count() != service.FirmStaffMemberIds.Count))
            return new IndependenceDecision(IndependenceDecisionCode.ServiceHistoryInvalid, ruleSet.Version,
                partnerEvaluationRecorded, examinationPeriodStart, ruleSet.LookBackMonths);

        DateOnly windowStart;
        try
        {
            windowStart = examinationPeriodStart.AddMonths(-ruleSet.LookBackMonths);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new IndependenceDecision(IndependenceDecisionCode.EvaluationInputInvalid);
        }
        var recent = clientHistory
            .Where(record => record.StartedOn <= examinationPeriodStart)
            .Where(record => record.EndedOn is null || record.EndedOn.Value >= windowStart)
            .ToArray();

        var assessments = new List<IndependenceServiceAssessment>(recent.Length);
        var hasUnclassifiedService = false;
        var hasImpairingService = false;
        var hasConditionalService = false;
        foreach (var service in recent)
        {
            if (!ruleSet.TryGetClassification(service.ServiceType, service.InvolvedManagementFunctions,
                    out var classification))
            {
                assessments.Add(new IndependenceServiceAssessment(service, null));
                hasUnclassifiedService = true;
                continue;
            }

            assessments.Add(new IndependenceServiceAssessment(service, classification));
            hasImpairingService |= classification == IndependenceServiceClassification.Impairing;
            hasConditionalService |= classification == IndependenceServiceClassification.ConditionallyCompatible;
        }

        var outcome = hasUnclassifiedService
            ? (IndependenceEvaluationOutcome?)null
            : hasImpairingService
                ? IndependenceEvaluationOutcome.Impaired
                : hasConditionalService
                    ? IndependenceEvaluationOutcome.ConditionallyCompatible
                    : IndependenceEvaluationOutcome.Compatible;
        var code = hasUnclassifiedService
            ? IndependenceDecisionCode.ServiceNotClassified
            : hasImpairingService
                ? IndependenceDecisionCode.RecentImpairingService
                : hasConditionalService && !partnerEvaluationRecorded
                    ? IndependenceDecisionCode.PartnerEvaluationRequired
                    : IndependenceDecisionCode.Allowed;
        return new IndependenceDecision(code, ruleSet.Version, partnerEvaluationRecorded,
            examinationPeriodStart, ruleSet.LookBackMonths, outcome, assessments);
    }
}
