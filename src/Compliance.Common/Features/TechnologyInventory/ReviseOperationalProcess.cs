using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.operational_process.revise", 1)]
public sealed record ReviseOperationalProcess(Uuid TenantId, Uuid OperationalProcessId,
    long ExpectedRevision, string Name, string Purpose, Uuid OwnerPersonId, string Lifecycle,
    string? Inputs = null, string? Outputs = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable;
