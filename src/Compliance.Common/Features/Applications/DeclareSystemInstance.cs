using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.declare", 1)]
public sealed record DeclareSystemInstance(Uuid TenantId, Uuid ApplicationId,
    long ExpectedApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference = null, string? SourceIdentifier = null)
    : IRequest<SystemInstanceRegistration>, IApplicationInventoryRequest, ICallable;
