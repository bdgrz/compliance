using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Records a personal acknowledgement. A workforce person correlated to the acting member
///     acknowledges for themselves; a person without a membership may have it recorded by a
///     program manager, attributed separately as performer and recorder. HTTP-only.
/// </summary>
public sealed class AcknowledgePolicyHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAccessGrantPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<AcknowledgePolicy, CampaignAcknowledgementView>
{
    public async ValueTask<Result<CampaignAcknowledgementView>> HandleAsync(
        IRequestContext<AcknowledgePolicy> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await CampaignSource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.CampaignId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CampaignAcknowledgementView>.Failure(source.Error);
        var actor = PolicyActor.From(context, request.TenantId);
        var person = await reader.HydrateAsync(new Person(request.TenantId, request.PersonId), ct)
            .ConfigureAwait(false);
        // Authorize before consulting the audience, so a member acting for someone else learns
        // nothing about who is in it or who holds a membership.
        var self = person.CorrelatedUserId == actor.UserId;
        if (!self && !await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                request.ProgramId, IProgramScopedRequest.ManagementPermission, ct).ConfigureAwait(false))
            return NotInAudience();
        if (!source.Value.Contains(request.PersonId))
            return NotInAudience();
        if (!self && person.CorrelatedUserId is not null)
            return Result<CampaignAcknowledgementView>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "A member must acknowledge a policy personally."));
        var onBehalf = !self;
        var performer = new ActorReference("workforce_person", request.PersonId.ToString(),
            source.Value.DisplayNameOf(request.PersonId) ?? "Workforce person");
        return await executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                request.CampaignId),
            campaign =>
            {
                var failure = campaign.Acknowledge(request.ProgramId, context.RequestId,
                    request.PersonId, request.PolicyVersion, request.ContentSha256,
                    request.AcknowledgementText, performer, actor.Reference, onBehalf,
                    clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    campaign.FindAcknowledgement(request.PersonId)!);
            }, context, ct).ConfigureAwait(false);
    }

    static Result<CampaignAcknowledgementView> NotInAudience() =>
        Result<CampaignAcknowledgementView>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The person is not in this campaign's current audience."));
}
