using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A component revision as recorded in the governed manual inventory.</summary>
public sealed record TechnologyComponentView(Uuid TenantId, Uuid ComponentId, long Revision,
    TechnologyComponentContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
