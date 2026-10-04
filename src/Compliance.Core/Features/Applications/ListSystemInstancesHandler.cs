using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListSystemInstancesHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListSystemInstances, Page<SystemInstanceView>>
{
    public async ValueTask<Result<Page<SystemInstanceView>>> HandleAsync(
        IRequestContext<ListSystemInstances> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance list limit must be between 1 and 200."));
        if (request.MinimumApplicationRevision is < 1)
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum application revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var canReadApplication = await visibility.CanReadApplicationAsync(request.TenantId,
            userId, request.ApplicationId, ct).ConfigureAwait(false);
        Page<SystemInstanceView> page;
        try
        {
            page = await VisibleApplicationPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListInstancesAsync(request.TenantId,
                    request.ApplicationId, limit, cursor, ct),
                item => visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                    item.ApplicationId, item.SystemInstanceId, ct),
                item => item.TenantId == request.TenantId &&
                        item.ApplicationId == request.ApplicationId);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The system instance cursor is invalid."));
        }
        catch (VisibleApplicationPage.ForeignDirectoryItemException)
        {
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instances were not found."));
        }
        if (!canReadApplication && page.Items.Count == 0)
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instances were not found."));

        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, null, null, ct).ConfigureAwait(false);
        return !freshness.IsSuccess
            ? Result<Page<SystemInstanceView>>.Failure(freshness.Error)
            : Result<Page<SystemInstanceView>>.Success(page);
    }
}
