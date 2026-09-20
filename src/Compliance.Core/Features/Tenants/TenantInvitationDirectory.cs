using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Only administrative display fields from the invitation stream.</summary>
public sealed record TenantInvitationDirectoryEntry(Uuid TenantId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, DateTimeOffset ExpiresAt,
    Uuid InvitedBy, Uuid? AcceptedUserId);

public interface ITenantInvitationDirectoryReader
{
    ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId, string emailAddress,
        CancellationToken ct = default);
    ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
}

public interface ITenantInvitationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
