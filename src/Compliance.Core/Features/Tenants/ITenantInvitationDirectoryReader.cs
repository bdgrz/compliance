using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public interface ITenantInvitationDirectoryReader
{
    ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId, string emailAddress,
        CancellationToken ct = default);
    ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
}
