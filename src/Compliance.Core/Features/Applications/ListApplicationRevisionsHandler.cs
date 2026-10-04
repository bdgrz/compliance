using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationRevisionsHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListApplicationRevisions, Page<ApplicationRevisionView>>
{
    public async ValueTask<Result<Page<ApplicationRevisionView>>> HandleAsync(
        IRequestContext<ListApplicationRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application revision list limit must be between 1 and 200."));
        if (request.MinimumApplicationRevision is < 1)
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum application revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        var freshness = await consistency.EnsureAsync(request.TenantId,
                request.ApplicationId, request.MinimumApplicationRevision, ct)
            .ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ApplicationRevisionView>>.Failure(freshness.Error);
        Page<ApplicationRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId,
                    request.ApplicationId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application revision cursor is invalid."));
        }
        if (page is null || page.Items.Any(item => item.TenantId != request.TenantId ||
                                                   item.ApplicationId != request.ApplicationId))
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application revision projection is incomplete."));
        return Result<Page<ApplicationRevisionView>>.Success(page);
    }
}
