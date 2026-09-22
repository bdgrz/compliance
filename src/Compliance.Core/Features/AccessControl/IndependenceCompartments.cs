using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The practice a firm-staff engagement assignment serves (M0-D26).</summary>
public enum EngagementPractice
{
    Advisory,
    Attest,
}

/// <summary>An advisory service the firm delivered to a client, as it bears on attest independence.</summary>
public enum AdvisoryService
{
    ReadinessAssessment,
    ControlDesign,
    ControlImplementation,
    ControlOperation,
}

/// <summary>The independence compartment a client record belongs to.</summary>
public enum RecordCompartment
{
    Shared,
    AdvisoryWorkingNotes,
}

/// <summary>One person's assignment to one client engagement.</summary>
public sealed record EngagementAssignment(
    Uuid ClientTenantId,
    Uuid EngagementId,
    Uuid UserId,
    EngagementPractice Practice);

/// <summary>A past or ongoing advisory engagement for the client an attest engagement would examine.</summary>
public sealed record AdvisoryEngagementRecord(AdvisoryService Service, DateOnly? EndedOn);

/// <summary>
///     The M0-D26 strict independence wall between advisory and attest work for one client, as in-code
///     rules that engagement assignment, record authorizers, and engagement acceptance apply.
/// </summary>
public static class IndependenceCompartments
{
    const int LookBackMonths = 12;

    /// <summary>One person may never hold both an advisory and an attest assignment for the same client.</summary>
    public static Result CanAssign(IEnumerable<EngagementAssignment> clientAssignments,
        EngagementAssignment candidate)
    {
        ArgumentNullException.ThrowIfNull(clientAssignments);
        ArgumentNullException.ThrowIfNull(candidate);
        var crossesWall = clientAssignments.Any(existing =>
            existing.ClientTenantId == candidate.ClientTenantId &&
            existing.UserId == candidate.UserId &&
            existing.Practice != candidate.Practice);
        return crossesWall
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A person cannot hold both advisory and attest assignments for the same client."))
            : Result.Success;
    }

    /// <summary>Attest assignees for a client cannot read that client's advisory working notes.</summary>
    public static bool CanRead(Uuid clientTenantId, IEnumerable<EngagementAssignment> actorAssignments,
        RecordCompartment compartment)
    {
        ArgumentNullException.ThrowIfNull(actorAssignments);
        return compartment != RecordCompartment.AdvisoryWorkingNotes ||
            !actorAssignments.Any(assignment =>
                assignment.ClientTenantId == clientTenantId &&
                assignment.Practice == EngagementPractice.Attest);
    }

    /// <summary>
    ///     An attest engagement cannot be accepted for a client that received control design,
    ///     implementation, or operation from the firm within the last twelve months, including
    ///     ongoing work. A readiness assessment in the same window requires a recorded partner evaluation.
    /// </summary>
    public static Result CanAcceptAttestEngagement(IEnumerable<AdvisoryEngagementRecord> advisoryHistory,
        DateOnly acceptedOn, bool partnerEvaluationRecorded)
    {
        ArgumentNullException.ThrowIfNull(advisoryHistory);
        var windowStart = acceptedOn.AddMonths(-LookBackMonths);
        var recent = advisoryHistory
            .Where(record => record.EndedOn is null || record.EndedOn.Value >= windowStart)
            .ToArray();
        if (recent.Any(record => record.Service is not AdvisoryService.ReadinessAssessment))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The firm designed, implemented, or operated this client's controls within the look-back period."));
        if (recent.Length > 0 && !partnerEvaluationRecorded)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A recent readiness assessment requires a documented partner independence evaluation."));
        return Result.Success;
    }
}
