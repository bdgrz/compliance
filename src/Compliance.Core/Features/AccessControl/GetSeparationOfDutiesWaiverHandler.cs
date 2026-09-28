using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetSeparationOfDutiesWaiverHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>
{
    public async ValueTask<Result<SeparationOfDutiesWaiverView>> HandleAsync(
        IRequestContext<GetSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
            request.WaiverId), ct).ConfigureAwait(false);
        return waiver.ViewOrNull(clock.GetUtcNow()) is { } view
            ? Result<SeparationOfDutiesWaiverView>.Success(view)
            : Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The separation-of-duties waiver was not found."));
    }
}
