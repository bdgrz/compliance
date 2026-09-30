using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>An information asset revision as recorded in the governed manual inventory.</summary>
public sealed record InformationAssetView(Uuid TenantId, Uuid InformationAssetId,
    long Revision, InformationAssetContent Content, string SourceKind,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt);
