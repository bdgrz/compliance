using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class GetBoundaryDecisionHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetBoundaryDecision, BoundaryDecisionView>
{
    public async ValueTask<Result<BoundaryDecisionView>> HandleAsync(
        IRequestContext<GetBoundaryDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var decision = await directory.GetDecisionAsync(request.TenantId,
            request.BoundaryId, request.DecisionId, ct).ConfigureAwait(false);
        return decision is null
            ? Result<BoundaryDecisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary decision was not found."))
            : Result<BoundaryDecisionView>.Success(decision);
    }
}
