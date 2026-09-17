using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Lists the tenants the calling user belongs to, as owner or member — always scoped to the
///     caller's own Bdgrz identity, never an arbitrary user. <c>Limit</c> defaults to 50 and must be
///     between 1 and 200; <c>Cursor</c> resumes a previous page; memberships have exactly one index,
///     so <c>Sort</c> only ever toggles direction (anything containing <c>"desc"</c> reverses it).
/// </summary>
[Discriminator("bdgrz.tenant-membership.list-mine", 1)]
public sealed record ListMyTenants(
    int? Limit = null,
    string? Cursor = null,
    string? Sort = null) : IRequest<Page<TenantMembershipSummary>>, ICallable;
