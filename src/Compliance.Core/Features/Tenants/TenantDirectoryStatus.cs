using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

static class TenantDirectoryStatus
{
    public static string? After(TenantView current, DomainEvent domainEvent)
    {
        if (current.Status == "offboarding")
            return "offboarding";
        if (domainEvent is TenantActivated && current.Status == "suspended")
            return "suspended";

        return domainEvent switch
        {
            TenantSlugConfirmed when current.RequiresActivation => "provisioning",
            TenantSlugConfirmed or TenantReactivated or TenantActivated => "active",
            TenantSlugRejected => "rejected",
            TenantSuspended => "suspended",
            TenantOffboardingStarted => "offboarding",
            _ => null,
        };
    }
}
