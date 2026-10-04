using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class ListAccessExpectationsHandler(IAggregateReader reader,
    IApplicationDirectoryReader applications, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListAccessExpectations, AccessExpectationsView>
{
    public async ValueTask<Result<AccessExpectationsView>> HandleAsync(
        IRequestContext<ListAccessExpectations> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await applications.GetInstanceAsync(request.TenantId,
            request.SystemInstanceId, ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (instance is null || !await visibility.CanReadSystemInstanceAsync(request.TenantId,
                userId, instance.ApplicationId, request.SystemInstanceId, ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationsView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(request.TenantId,
            request.SystemInstanceId), ct).ConfigureAwait(false);
        return Result<AccessExpectationsView>.Success(ledger.ToView());
    }
}
