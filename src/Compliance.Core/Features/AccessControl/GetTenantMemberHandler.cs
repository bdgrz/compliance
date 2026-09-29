using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetTenantMemberHandler(ITenantMembershipDirectoryReader memberships)
    : IRequestHandler<GetTenantMember, TenantMembershipView>
{
    public async ValueTask<Result<TenantMembershipView>> HandleAsync(
        IRequestContext<GetTenantMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var membership = await memberships.GetAsync(request.TenantId.ToString(), request.UserId, ct)
            .ConfigureAwait(false);
        return membership is null || membership.TenantId != request.TenantId
            ? Result<TenantMembershipView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."))
            : Result<TenantMembershipView>.Success(membership);
    }
}
