using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListProgramsHandler(IProgramDirectoryReader directory,
    IAccessGrantPermissionAuthorizer access)
    : IRequestHandler<ListPrograms, Page<ProgramView>>
{
    public async ValueTask<Result<Page<ProgramView>>> HandleAsync(
        IRequestContext<ListPrograms> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program list limit must be between 1 and 200."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Program discovery requires a Bdgrz user identity."));
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
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The program cursor is invalid."));
            }
        }

        var programs = new List<ProgramView>();
        var cursor = request.Cursor;
        do
        {
            var remaining = (request.Limit ?? 50) - programs.Count;
            Page<ProgramView> page;
            try
            {
                page = await directory.ListAsync(request.TenantId, remaining, cursor, ct)
                    .ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The program cursor is invalid."));
            }
            if (page.Items.Any(item => item.TenantId != request.TenantId))
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The programs were not found."));

            programs.AddRange(page.Items.Where(item => visibility.ProgramIds.Contains(item.ProgramId)));
            cursor = page.NextCursor;
        } while (programs.Count < (request.Limit ?? 50) && cursor is not null);

        return Result<Page<ProgramView>>.Success(new Page<ProgramView>(programs, cursor));
    }

    static Result<Page<ProgramView>> ValidatePage(Uuid tenantId, Page<ProgramView> page) =>
        page.Items.Any(item => item.TenantId != tenantId)
            ? Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The programs were not found."))
            : Result<Page<ProgramView>>.Success(page);
}
