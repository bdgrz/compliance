using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads the acting member's own weekly email digest preference.</summary>
public sealed class GetWorkDigestPreferenceHandler(IAggregateReader reader)
    : IRequestHandler<GetWorkDigestPreference, WorkDigestPreferenceView>
{
    public async ValueTask<Result<WorkDigestPreferenceView>> HandleAsync(
        IRequestContext<GetWorkDigestPreference> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var preference = await reader.HydrateAsync(new WorkDigestPreference(request.TenantId,
            actor.MemberId), ct).ConfigureAwait(false);
        return Result<WorkDigestPreferenceView>.Success(preference.Read());
    }
}
