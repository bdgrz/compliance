using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationsHandler(IApplicationDirectoryReader directory,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListApplications, Page<ApplicationView>>
{
    public async ValueTask<Result<Page<ApplicationView>>> HandleAsync(
        IRequestContext<ListApplications> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application list limit must be between 1 and 200."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        Page<ApplicationView> page;
        try
        {
            page = await VisibleApplicationPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListAsync(request.TenantId, limit, cursor, ct),
                item => visibility.CanReadApplicationAsync(request.TenantId, userId,
                    item.ApplicationId, ct),
                item => item.TenantId == request.TenantId);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application cursor is invalid."));
        }
        catch (VisibleApplicationPage.ForeignDirectoryItemException)
        {
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The applications were not found."));
        }
        return Result<Page<ApplicationView>>.Success(page);
    }
}
