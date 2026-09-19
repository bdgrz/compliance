using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-member.list", 1)]
public sealed record ListTenantMembers(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<TenantMembershipView>>, ICallable, IPlatformOperatorRequest;
