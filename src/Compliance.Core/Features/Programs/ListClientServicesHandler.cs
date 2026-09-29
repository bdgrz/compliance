using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListClientServicesHandler(IClientServiceDirectoryReader directory,
    IAccessGrantPermissionAuthorizer access)
    : IRequestHandler<ListClientServices, Page<ClientServiceView>>
{
    public async ValueTask<Result<Page<ClientServiceView>>> HandleAsync(
        IRequestContext<ListClientServices> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The client service list limit must be between 1 and 200."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Client service discovery requires a Bdgrz user identity."));
        var visibility = await access.GetProgramVisibilityAsync(request.TenantId, userId,
                RbacIds.Member(request.TenantId, userId), IProgramReadRequest.ReadPermission, ct)
            .ConfigureAwait(false);

        if (visibility.OrganizationWide)
        {
            try
            {
                var page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                    request.Cursor, ct).ConfigureAwait(false);
                return ValidatePage(request.TenantId, page);
            }
            catch (KvDirectoryQueryException)
            {
                return CursorError();
            }
        }

        var services = new List<ClientServiceView>();
        var cursor = request.Cursor;
        do
        {
            var remaining = (request.Limit ?? 50) - services.Count;
            Page<ClientServiceView> page;
            try
            {
                page = await directory.ListAsync(request.TenantId, remaining, cursor, ct)
                    .ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return CursorError();
            }
            if (page.Items.Any(item => item.TenantId != request.TenantId))
                return NotFoundError();

            services.AddRange(page.Items.Where(item => item.ProgramId is { } programId &&
                visibility.ProgramIds.Contains(programId)));
            cursor = page.NextCursor;
        } while (services.Count < (request.Limit ?? 50) && cursor is not null);

        return Result<Page<ClientServiceView>>.Success(new Page<ClientServiceView>(services, cursor));
    }

    static Result<Page<ClientServiceView>> ValidatePage(Uuid tenantId, Page<ClientServiceView> page) =>
        page.Items.Any(item => item.TenantId != tenantId) ? NotFoundError()
            : Result<Page<ClientServiceView>>.Success(page);

    static Result<Page<ClientServiceView>> CursorError() =>
        Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.Validation,
            "The client service cursor is invalid."));

    static Result<Page<ClientServiceView>> NotFoundError() =>
        Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The services were not found."));
}
