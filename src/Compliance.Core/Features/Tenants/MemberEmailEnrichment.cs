using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Adds each member's verified platform email so administrators can tell members apart.</summary>
static class MemberEmailEnrichment
{
    public static async ValueTask<TenantMembershipView> WithEmailAsync(
        IEmailAddressDirectoryReader? emails, TenantMembershipView member, CancellationToken ct)
    {
        if (emails is null)
            return member;
        var addresses = await emails.ListAsync(member.UserId, 20, null, ct).ConfigureAwait(false);
        var verified = addresses.Items
            .Where(static address => address.Verified)
            .Select(static address => address.EmailAddress)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        return member with { VerifiedEmailAddress = verified };
    }
}
