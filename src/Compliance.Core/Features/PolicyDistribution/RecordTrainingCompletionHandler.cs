using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class RecordTrainingCompletionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordTrainingCompletion, CampaignCompletionView>
{
    public ValueTask<Result<CampaignCompletionView>> HandleAsync(
        IRequestContext<RecordTrainingCompletion> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                request.CampaignId),
            campaign =>
            {
                var failure = campaign.RecordCompletion(request.ProgramId, context.RequestId,
                    request.PersonId, request.RequirementVersion, request.CompletedOn,
                    request.Source, request.EvidenceReference, actor.Reference,
                    clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    campaign.FindCompletion(request.PersonId)!);
            }, context, ct);
    }
}
