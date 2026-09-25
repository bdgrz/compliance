using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-invitation.list", 1)]
public sealed record ListTenantInvitations(Uuid TenantId, int? Limit = null,
    string? Cursor = null, string? EmailAddress = null)
    : IRequest<Page<TenantInvitationView>>, IRbacManagementRequest, ICallable;
