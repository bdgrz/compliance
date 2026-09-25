using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.retire", 1)]
public sealed record RetireClientService(Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Rationale) : IRequest, IProgramManagementRequest, ICallable;
