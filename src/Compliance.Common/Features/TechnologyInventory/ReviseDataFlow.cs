using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a new data-flow version effective no earlier than its predecessor.</summary>
[Discriminator("bdgrz.inventory.data_flow.revise", 1)]
public sealed record ReviseDataFlow(Uuid TenantId, Uuid DataFlowId, long ExpectedRevision,
    string SourceType, Uuid SourceId, string DestinationType,
    IReadOnlyList<Uuid> InformationAssetIds, string Purpose, bool EncryptedInTransit,
    bool EncryptedAtRest, DateOnly EffectiveFrom, Uuid OwnerPersonId, string Lifecycle,
    Uuid? DestinationId = null, string? DestinationParty = null,
    string? ExceptionReference = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable;
