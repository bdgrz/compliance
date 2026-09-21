using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Lists projected organization metadata for a platform operator, including provisioning and
///     suspended organizations. Results may lag the tenant event stream.
/// </summary>
[Discriminator("bdgrz.platform.tenant.list", 1)]
public sealed record ListTenants(int? Limit = null, string? Cursor = null)
    : IRequest<Page<TenantView>>, ICallable, IPlatformOperatorRequest;
