using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetTenantMemberHandler(ITenantMembershipDirectoryReader memberships,
    IEmailAddressDirectoryReader? emails = null)
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
            : Result<TenantMembershipView>.Success(
                await MemberEmailEnrichment.WithEmailAsync(emails, membership, ct).ConfigureAwait(false));
    }
}
