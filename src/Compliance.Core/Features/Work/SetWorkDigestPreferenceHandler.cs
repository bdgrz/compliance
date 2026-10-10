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
        if (!WorkDigestSchedule.IsValidTimeZone(request.TimeZoneId))
            return Result<WorkDigestPreferenceView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The time_zone_id is not a valid time zone."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new WorkDigestPreference(request.TenantId,
                actor.MemberId),
            preference =>
            {
                preference.Set(request.EmailDigestEnabled, request.TimeZoneId,
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow());
                return AggregateOutcome.Commit(Result<WorkDigestPreferenceView>.Success(
                    preference.Read()));
            }, context, ct).ConfigureAwait(false);
    }
}
