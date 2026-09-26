using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>A manually recorded workforce person. The source is the tenant's governed roster.</summary>
public sealed record PersonView(Uuid TenantId, Uuid PersonId, long Revision,
    string DisplayName, string? WorkEmail, string SourceKind,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt);
