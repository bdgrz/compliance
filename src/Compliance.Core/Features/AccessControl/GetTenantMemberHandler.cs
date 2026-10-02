using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetTenantMemberHandler(ITenantMembershipDirectoryReader memberships,
    IEmailAddressDirectoryReader? emails = null, IUserDisplayNameReader? profiles = null,
    IPersonMemberDisplayReader? people = null)
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
                await MemberPresentationEnrichment.WithMemberAsync(emails, membership, ct,
                    profiles, people).ConfigureAwait(false));
    }
}
