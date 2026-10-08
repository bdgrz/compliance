using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Revises a component. Category and system instance identity are immutable.</summary>
[Discriminator("bdgrz.inventory.component.revise", 1)]
public sealed record ReviseTechnologyComponent(Uuid TenantId, Uuid ComponentId,
    long ExpectedRevision, string Name, Uuid OwnerPersonId, string Lifecycle,
    string? EnvironmentReference = null, string? LocationReference = null,
    int? EndpointCount = null, string? ManagementSource = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable;
