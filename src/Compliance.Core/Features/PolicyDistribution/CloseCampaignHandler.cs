using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class CloseCampaignHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CloseCampaign, CampaignView>
{
    public ValueTask<Result<CampaignView>> HandleAsync(IRequestContext<CloseCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        return executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                request.CampaignId),
            campaign =>
            {
                var failure = campaign.Close(request.ProgramId, request.Rationale,
                    actor.Reference, now);
                return failure is null
                    ? AggregateOutcome.Commit(Result<CampaignView>.Success(
                        campaign.ToView(DateOnly.FromDateTime(now.UtcDateTime))))
                    : CommandFailureRequestAdapter.ToOutcome<CampaignView>(failure, null!);
            }, context, ct);
    }
}
