using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a governed, effective-dated data flow in the tenant's manual inventory.</summary>
[Discriminator("bdgrz.inventory.data_flow.record", 1)]
public sealed record RecordDataFlow(Uuid TenantId, string SourceType, Uuid SourceId,
    string DestinationType, IReadOnlyList<Uuid> InformationAssetIds, string Purpose,
    bool EncryptedInTransit, bool EncryptedAtRest, DateOnly EffectiveFrom, Uuid OwnerPersonId,
    Uuid? DestinationId = null, string? DestinationParty = null,
    string? ExceptionReference = null)
    : IRequest<DataFlowRegistration>, ITechnologyInventoryRequest, ICallable;
