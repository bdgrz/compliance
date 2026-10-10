using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class AcceptServiceEngagementHandler(IAggregateExecutor executor,
    IAggregateReader reader, IServiceEngagementAcceptanceEvidenceReader evidenceReader,
    IPlatformUserDirectoryReader platformUsers, ITenantActivity tenants,
    ProfessionalDutyAuthorityReader duties, CurrentRatifiedIndependenceRulesReader rulesReader,
    TimeProvider clock)
    : IRequestHandler<AcceptServiceEngagement, ServiceEngagementAcceptanceView>
{
    public async ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<AcceptServiceEngagement> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Professional acceptance requires the assigned authenticated firm partner over personal HTTP."));

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            !await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false) ||
            !await platformUsers.ExistsAsync(actorUserId, ct).ConfigureAwait(false))
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The client tenant or canonical platform user is not currently eligible for professional acceptance."));

        var currentDuty = await duties.ReadCurrentAsync(actorUserId,
            FirmProfessionalDuty.EngagementPartner, request.TenantId, ct).ConfigureAwait(false);
        if (currentDuty is null)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor no longer has a current partner designation for this client."));

        var currentLedger = await reader.HydrateAsync(new IndependenceLedger(request.TenantId), ct)
            .ConfigureAwait(false);
        if (currentLedger.AcceptanceSourceForRequest(context.RequestId) is { } prior)
        {
            if (prior.ExpectedSequence != request.ExpectedSequence ||
                prior.Acceptance.TenantId != request.TenantId ||
                prior.Acceptance.EngagementId != request.EngagementId ||
                prior.Acceptance.PartnerUserId != actorUserId)
                return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The request identity already records a different engagement acceptance."));
            return Result<ServiceEngagementAcceptanceView>.Success(prior.Acceptance);
        }

        var evidence = await evidenceReader.ReadCurrentAsync(request.TenantId, request.EngagementId,
                actorUserId, ct)
            .ConfigureAwait(false);
        if (!evidence.IsSuccess)
            return Result<ServiceEngagementAcceptanceView>.Failure(evidence.Error!);
        if (evidence.Value is not { } current || current.Proof is not { } proof ||
            current.RatifiedRules is not { IsRatified: true, Version: > 0 } ||
            proof.TenantId != request.TenantId || proof.EngagementId != request.EngagementId)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Current scoped engagement authority and ratified rules could not be verified; acceptance was not recorded.", true));

        if (proof.PartnerUserId != actorUserId || proof.CurrentPartner is not { IsActive: true } currentPartner ||
            currentPartner.UserId != actorUserId || currentDuty.Staff != currentPartner ||
            proof.PartnerDutyRevision != currentDuty.Designation.Revision ||
            proof.AuthorityReference != currentDuty.Designation.SourceReference ||
            proof.PartnerEvaluation is { } evaluation && evaluation.DutyDesignationId != currentDuty.Designation.DesignationId)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the currently designated partner may accept this engagement."));

        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var currentDirectoryStaff = directory.View().Staff;
        var identities = currentDirectoryStaff.Where(staff => staff.UserId == actorUserId).ToArray();
        if (identities.Length != 1 || !identities[0].IsActive || identities[0] != currentPartner ||
            proof.PartnerStaffMemberId != currentPartner.StaffMemberId || proof.PartnerDutyRevision <= 0)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The partner identity or directory source changed; reload before accepting this engagement.", true));

        if (proof.CurrentStaff is null || proof.CurrentStaff.Any(assigned =>
                !MatchesCurrentDirectoryAssignment(assigned, currentDirectoryStaff)))
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An assigned team member no longer matches the same active current directory record; acceptance was not recorded.", true));

        var activeRules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        if (activeRules is null || activeRules.Version != current.RatifiedRules.Version ||
            IndependenceSourceDigest.RuleContent(activeRules.Content) !=
                IndependenceSourceDigest.RuleContent(current.RatifiedRules.Content) ||
            activeRules.Ratification?.RatificationId != current.RatifiedRules.Ratification?.RatificationId)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Ratified independence rules changed after evidence was read; reload before accepting.", true));
        var confirmedDuty = await duties.ReadCurrentAsync(actorUserId,
            FirmProfessionalDuty.EngagementPartner, request.TenantId, ct).ConfigureAwait(false);
        if (confirmedDuty != currentDuty)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The partner designation changed after evidence was read; reload before accepting.", true));

        return await executor.ExecuteAsync(new IndependenceLedger(request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.AcceptEngagement(context.RequestId,
                request.ExpectedSequence, proof, current.RatifiedRules, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }

    static bool MatchesCurrentDirectoryAssignment(FirmStaffMemberView? assigned,
        IReadOnlyList<FirmStaffMemberView> currentDirectoryStaff)
    {
        if (assigned is null)
            return false;

        var matches = currentDirectoryStaff.Where(directoryStaff =>
            directoryStaff.StaffMemberId == assigned.StaffMemberId || directoryStaff.UserId == assigned.UserId)
            .ToArray();
        return matches.Length == 1 && matches[0].IsActive && matches[0] == assigned;
    }
}
