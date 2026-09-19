using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantMembersHandler(ITenantMembershipDirectoryReader directory)
    : IRequestHandler<ListTenantMembers, Page<TenantMembershipView>>
{
    public async ValueTask<Result<Page<TenantMembershipView>>> HandleAsync(
        IRequestContext<ListTenantMembers> context, CancellationToken ct) =>
        Result<Page<TenantMembershipView>>.Success(await directory.ListAsync(
            context.Request.TenantId, Math.Clamp(context.Request.Limit ?? 50, 1, 200),
            context.Request.Cursor, ct).ConfigureAwait(false));
}
