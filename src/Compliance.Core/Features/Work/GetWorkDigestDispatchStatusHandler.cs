using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads the durable digest dispatch ledger without exposing message or transport data.</summary>
public sealed class GetWorkDigestDispatchStatusHandler(IAggregateReader reader)
    : IRequestHandler<GetWorkDigestDispatchStatus, WorkDigestDispatchStatusView>
{
    public async ValueTask<Result<WorkDigestDispatchStatusView>> HandleAsync(
        IRequestContext<GetWorkDigestDispatchStatus> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty)
            return Result<WorkDigestDispatchStatusView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The work digest dispatch scope is incomplete."));

        var dispatch = await reader.HydrateAsync(new WorkDigestDispatch(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        return Result<WorkDigestDispatchStatusView>.Success(dispatch.Read(request.WeekOf));
    }
}
