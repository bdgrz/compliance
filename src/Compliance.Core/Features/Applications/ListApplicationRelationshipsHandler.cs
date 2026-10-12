using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationRelationshipsHandler(IAggregateReader reader,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListApplicationRelationships, ApplicationRelationshipPage>
{
    public async ValueTask<Result<ApplicationRelationshipPage>> HandleAsync(
        IRequestContext<ListApplicationRelationships> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ApplicationId == Uuid.Empty ||
            request.Limit is < 1 or > 200 || request.MinimumRevision is < 0 ||
            request.Cursor is { Length: > 1024 })
            return Result<ApplicationRelationshipPage>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application relationship list query is invalid."));

        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
            request.ApplicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadApplicationAsync(request.TenantId,
                userId, request.ApplicationId, ct).ConfigureAwait(false))
            return NotFound();
        var projection = await ApplicationRelationshipProjection.ReadAsync(source,
            request.TenantId, userId, visibility, ct).ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum && minimum > projection.Revision)
            return Result<ApplicationRelationshipPage>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                $"The visible relationship revision {projection.Revision} is below " +
                $"requested minimum revision {minimum}; retry the read.",
                isTransient: true));

        var error = ApplicationRelationshipPaging.Create(request.TenantId,
            request.ApplicationId, userId, projection.Revision, projection.ActiveItems,
            projection.Fingerprint, request.Limit, request.Cursor, out var page);
        return error is not null
            ? Result<ApplicationRelationshipPage>.Failure(error)
            : Result<ApplicationRelationshipPage>.Success(page);
    }

    static Result<ApplicationRelationshipPage> NotFound() =>
        Result<ApplicationRelationshipPage>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The application was not found."));
}
