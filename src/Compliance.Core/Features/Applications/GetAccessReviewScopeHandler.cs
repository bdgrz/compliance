using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reads the effective scope decision and its retained history from the source stream.</summary>
public sealed class GetAccessReviewScopeHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetAccessReviewScope, AccessReviewScopeView>
{
    public async ValueTask<Result<AccessReviewScopeView>> HandleAsync(
        IRequestContext<GetAccessReviewScope> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await reader.HydrateAsync(new DeclaredSystemInstance(request.TenantId,
            request.SystemInstanceId), ct).ConfigureAwait(false);
        if (!instance.IsCreated || instance.ApplicationId != request.ApplicationId)
            return Result<AccessReviewScopeView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var scope = await reader.HydrateAsync(new SystemInstanceAccessReviewScope(
            request.TenantId, request.SystemInstanceId), ct).ConfigureAwait(false);
        return Result<AccessReviewScopeView>.Success(scope.ToView(request.ApplicationId,
            request.AsOf ?? clock.GetUtcNow()));
    }
}
