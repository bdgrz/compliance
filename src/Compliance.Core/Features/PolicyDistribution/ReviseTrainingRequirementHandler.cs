using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class ReviseTrainingRequirementHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<ReviseTrainingRequirement, TrainingRequirementRegistration>
{
    public ValueTask<Result<TrainingRequirementRegistration>> HandleAsync(
        IRequestContext<ReviseTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new TrainingCatalog(request.TenantId, request.ProgramId),
            catalog => AggregateOutcome.CommitOnSuccess(catalog.Revise(context.RequestId,
                request.RequirementId, request.ExpectedVersion, request.Content,
                actor.Reference, clock.GetUtcNow())),
            context, ct);
    }
}
