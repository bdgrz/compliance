using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class PublishRiskMethodVersionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<PublishRiskMethodVersion, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<PublishRiskMethodVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<RiskMethodVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The program was not found."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskMethod(request.TenantId, request.ProgramId),
            method =>
            {
                var failure = method.Publish(request.ProgramId, request.ExpectedVersion,
                    request.LikelihoodScale, request.ImpactScale, request.AppetiteThreshold,
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure, method.Current!);
            }, context, ct).ConfigureAwait(false);
    }
}
