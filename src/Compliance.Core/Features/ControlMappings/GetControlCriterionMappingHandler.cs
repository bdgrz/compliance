using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class GetControlCriterionMappingHandler(IAggregateReader reader)
    : IRequestHandler<GetControlCriterionMapping, ControlCriterionMappingView>
{
    public async ValueTask<Result<ControlCriterionMappingView>> HandleAsync(
        IRequestContext<GetControlCriterionMapping> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ledger.Read(request.MappingId) is { } view
            ? Result<ControlCriterionMappingView>.Success(view)
            : Result<ControlCriterionMappingView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The control criterion mapping was not found."));
    }
}
