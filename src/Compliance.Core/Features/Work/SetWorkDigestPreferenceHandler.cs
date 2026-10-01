using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the acting member's own weekly email digest preference.</summary>
public sealed class SetWorkDigestPreferenceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<SetWorkDigestPreference, WorkDigestPreferenceView>
{
    public async ValueTask<Result<WorkDigestPreferenceView>> HandleAsync(
        IRequestContext<SetWorkDigestPreference> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new WorkDigestPreference(request.TenantId,
                actor.MemberId),
            preference =>
            {
                preference.Set(request.EmailDigestEnabled,
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow());
                return AggregateOutcome.Commit(Result<WorkDigestPreferenceView>.Success(
                    preference.Read()));
            }, context, ct).ConfigureAwait(false);
    }
}
