using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A data-flow version as recorded in the governed manual inventory.</summary>
public sealed record DataFlowView(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
