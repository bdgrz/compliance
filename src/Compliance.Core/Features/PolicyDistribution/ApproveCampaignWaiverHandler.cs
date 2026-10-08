using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The Compliance Lead's exception approval. Until a dedicated role exists, the Compliance
///     Lead is an organization-wide program manager (as for M0-D05 scope approval). HTTP-only.
/// </summary>
public sealed class ApproveCampaignWaiverHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAccessGrantPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<ApproveCampaignWaiver, CampaignWaiverView>
{
    public async ValueTask<Result<CampaignWaiverView>> HandleAsync(
        IRequestContext<ApproveCampaignWaiver> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<CampaignWaiverView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Campaign exception approval requires personal HTTP submission."));
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var visibility = await permissions.GetProgramVisibilityAsync(request.TenantId,
            actor.UserId, actor.MemberId, IProgramScopedRequest.ManagementPermission, ct)
            .ConfigureAwait(false);
        if (!visibility.OrganizationWide)
            return Result<CampaignWaiverView>.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "Campaign exceptions require organization-wide program management."));
        var person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId), ct)
            .ConfigureAwait(false);
        if (person.CorrelatedUserId == actor.UserId)
            return Result<CampaignWaiverView>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "A member cannot approve their own exception."));
        return await executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                request.CampaignId),
            campaign =>
            {
                var failure = campaign.ApproveWaiver(request.ProgramId, context.RequestId,
                    request.PersonId, request.Reason, request.ExpiresOn, actor.Reference,
                    actor.MemberId, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    campaign.FindWaiver(request.PersonId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
