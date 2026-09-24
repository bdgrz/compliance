using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Reports every actor as a member with one affiliation, or none as members.</summary>
sealed class FixedMembershipDirectory(bool member, string affiliation = "client_personnel")
    : ITenantMembershipDirectoryReader
{
    public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
        CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(member
        ? new TenantMembershipView(userId, Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
            affiliation)
        : null);

    public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
        CancellationToken ct = default) => ValueTask.FromResult(member);

    public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        ValueTask.FromResult(new Page<TenantMembershipView>([], null));
}
