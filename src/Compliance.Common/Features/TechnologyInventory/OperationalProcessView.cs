using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A operational process revision as recorded in the governed tenant inventory.</summary>
public sealed record OperationalProcessView(Uuid TenantId, Uuid OperationalProcessId, long Revision,
    OperationalProcessContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
