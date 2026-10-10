using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Resolves professional authority only from explicit current duty evidence and the active canonical directory.</summary>
sealed class ProfessionalDutyAuthorityReader(IAggregateReader reader,
    IPlatformUserDirectoryReader users, TimeProvider clock)
{
    public async ValueTask<CurrentProfessionalDuty?> ReadCurrentAsync(Uuid userId, string duty,
        Uuid? tenantId, CancellationToken ct)
    {
        if (userId == Uuid.Empty || !await users.ExistsAsync(userId, ct).ConfigureAwait(false))
            return null;
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var identities = directory.View().Staff.Where(item => item.UserId == userId).ToArray();
        if (identities is not [{ IsActive: true } staff])
            return null;
        var catalog = await reader.HydrateAsync(new FirmProfessionalDutyCatalog(), ct).ConfigureAwait(false);
        if (catalog.ActiveDesignation(userId, duty, tenantId, clock.GetUtcNow()) is not { } designation ||
            designation.StaffMemberId != staff.StaffMemberId ||
            designation.DirectoryStaffRevision != staff.Revision || !designation.IsActive)
            return null;
        return new CurrentProfessionalDuty(staff, designation);
    }
}
