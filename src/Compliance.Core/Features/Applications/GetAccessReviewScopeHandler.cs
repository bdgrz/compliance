using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reads the effective scope decision and its retained history from the source stream.</summary>
public sealed class GetAccessReviewScopeHandler(IAggregateReader reader, IDomainEventReader events,
    TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessReviewScope, AccessReviewScopeView>
{
    public async ValueTask<Result<AccessReviewScopeView>> HandleAsync(
        IRequestContext<GetAccessReviewScope> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events,
            request.TenantId, request.ApplicationId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        if (instance is null)
            return Result<AccessReviewScopeView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false))
            return Result<AccessReviewScopeView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var scope = await reader.HydrateAsync(new SystemInstanceAccessReviewScope(
            request.TenantId, request.SystemInstanceId), ct).ConfigureAwait(false);
        return Result<AccessReviewScopeView>.Success(scope.ToView(request.ApplicationId,
            request.AsOf ?? clock.GetUtcNow()));
    }
}
