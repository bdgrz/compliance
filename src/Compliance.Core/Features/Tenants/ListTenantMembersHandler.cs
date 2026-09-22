using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantMembersHandler(ITenantMembershipDirectoryReader directory)
    : IRequestHandler<ListTenantMembers, Page<TenantMembershipView>>
{
    public async ValueTask<Result<Page<TenantMembershipView>>> HandleAsync(
        IRequestContext<ListTenantMembers> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TenantMembershipView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The tenant member list limit must be between 1 and 200."));
        Page<TenantMembershipView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TenantMembershipView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The tenant member cursor is invalid."));
        }
        return page.Items.Any(member => member.TenantId != request.TenantId)
            ? Result<Page<TenantMembershipView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant members were not found."))
            : Result<Page<TenantMembershipView>>.Success(page);
    }
}
