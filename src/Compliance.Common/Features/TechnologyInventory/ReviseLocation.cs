using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Revises a location. Its kind is immutable.</summary>
[Discriminator("bdgrz.inventory.location.revise", 1)]
public sealed record ReviseLocation(Uuid TenantId, Uuid LocationId, long ExpectedRevision,
    string Name, Uuid OwnerPersonId, string Lifecycle, string? GeographyReference = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable;
