using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks the existing accepted assignment path for advisory-note reads.</summary>
sealed class ProfessionalAdvisoryReadAccess(IAggregateReader reader, ITenantActivity tenants,
    IProgramResourceScopeResolver programScopes, TimeProvider clock)
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid programId, Uuid userId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false) ||
            !await programScopes.IsTenantProgramAsync(tenantId, programId, ct).ConfigureAwait(false))
            return false;

        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var identities = directory.View().Staff.Where(staff => staff.UserId == userId).ToArray();
        if (identities.Length != 1 || !identities[0].IsActive || identities[0].Practice != "advisory" ||
            identities[0].StaffMemberId == Uuid.Empty || identities[0].Revision <= 0)
            return false;

        var staff = identities[0];
        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        var effectiveAt = clock.GetUtcNow();
        return ledger.Engagements.Any(engagement => engagement.TenantId == tenantId &&
            engagement.Content.Practice == "advisory" && engagement.Status == "accepted" &&
            ledger.Acceptance(engagement.EngagementId) is { Status: "active" } acceptance &&
            acceptance.TenantId == tenantId && acceptance.Rules.IsRatified &&
            ledger.IsEligibleForProfessionalAccess(engagement.EngagementId, staff.StaffMemberId,
                userId, currentRuleVersion: acceptance.Rules.Version,
                currentDirectoryStaffRevision: staff.Revision, effectiveAt: effectiveAt));
    }
}
