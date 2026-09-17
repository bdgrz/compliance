using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Lists the tenants the calling user belongs to, as owner or member — always scoped to the
///     caller's own Bdgrz identity, never an arbitrary user. Membership is stored per tenant, not in
///     one cross-tenant index, so this checks every currently active tenant rather than scanning a
///     bounded KV range; <c>Limit</c> (default 200, max 200) caps how many matching tenants are
///     returned, not how much work the check does.
/// </summary>
[Discriminator("bdgrz.tenant-membership.list-mine", 1)]
public sealed record ListMyTenants(int? Limit = null) : IRequest<Page<TenantMembershipSummary>>, ICallable;
