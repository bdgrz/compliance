using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A location revision as recorded in the governed tenant inventory.</summary>
public sealed record LocationView(Uuid TenantId, Uuid LocationId, long Revision,
    LocationContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
