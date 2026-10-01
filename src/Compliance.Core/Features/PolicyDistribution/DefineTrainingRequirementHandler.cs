using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class DefineTrainingRequirementHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<DefineTrainingRequirement, TrainingRequirementRegistration>
{
    public ValueTask<Result<TrainingRequirementRegistration>> HandleAsync(
        IRequestContext<DefineTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new TrainingCatalog(request.TenantId, request.ProgramId),
            catalog => AggregateOutcome.CommitOnSuccess(catalog.Define(context.RequestId,
                request.Identifier, request.Content, actor.Reference, clock.GetUtcNow())),
            context, ct);
    }
}
