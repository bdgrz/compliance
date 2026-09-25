using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Only administrative display fields from the invitation stream.</summary>
public sealed record TenantInvitationDirectoryEntry(Uuid TenantId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, DateTimeOffset ExpiresAt,
    Uuid InvitedBy, Uuid? AcceptedUserId)
{
    public string DeliveryStatus { get; init; } = "pending";
}
