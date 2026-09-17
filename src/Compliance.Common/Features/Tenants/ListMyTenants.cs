using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Lists the tenants the calling user belongs to, as owner or member — always scoped to the
///     caller's own Bdgrz identity, never an arbitrary user. Membership is stored per tenant, not in
///     one cross-tenant index, so this scans active tenants in lexicographic ID order rather than a
///     bounded KV range; <c>Limit</c> defaults to 50 and must be between 1 and 200; <c>Cursor</c>
///     resumes the tenant scan (not just matched results) after a previous page's last tenant ID.
/// </summary>
[Discriminator("bdgrz.tenant-membership.list-mine", 1)]
public sealed record ListMyTenants(
    int? Limit = null,
    string? Cursor = null) : IRequest<Page<TenantMembershipSummary>>, ICallable;
