using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListServiceIdentitiesHandler(IServiceIdentityDirectoryReader directory,
    ServiceIdentityReadConsistency consistency, ServiceIdentityOwnership ownership)
    : IRequestHandler<ListServiceIdentities, Page<ServiceIdentityView>>
{
    public async ValueTask<Result<Page<ServiceIdentityView>>> HandleAsync(
        IRequestContext<ListServiceIdentities> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The service identity list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ServiceIdentityView>>.Failure(ready.Error);
        Page<ServiceIdentityView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The service identity cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId))
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The service identities were not found."));
        var evaluated = await ownership.EvaluateAsync(request.TenantId, page.Items, ct)
            .ConfigureAwait(false);
        if (!evaluated.IsSuccess)
            return Result<Page<ServiceIdentityView>>.Failure(evaluated.Error);
        return Result<Page<ServiceIdentityView>>.Success(new Page<ServiceIdentityView>(
            [.. evaluated.Value.Where(item => request.UnownedOnly != true || item.Unowned)],
            page.NextCursor));
    }
}
