using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AcceptServiceEngagementHandler(IAggregateExecutor executor,
    IAggregateReader reader, IServiceEngagementAcceptanceEvidenceReader evidenceReader,
    IPlatformUserDirectoryReader platformUsers, ITenantActivity tenants, TimeProvider clock)
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
            currentPartner.UserId != actorUserId)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the currently designated partner may accept this engagement."));

        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var identities = directory.View().Staff.Where(staff => staff.UserId == actorUserId).ToArray();
        if (identities.Length != 1 || !identities[0].IsActive || identities[0] != currentPartner ||
            proof.PartnerStaffMemberId != currentPartner.StaffMemberId || proof.PartnerDutyRevision <= 0)
            return Result<ServiceEngagementAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The partner identity or directory source changed; reload before accepting this engagement.", true));

        return await executor.ExecuteAsync(new IndependenceLedger(request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.AcceptEngagement(context.RequestId,
                request.ExpectedSequence, proof, current.RatifiedRules, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
