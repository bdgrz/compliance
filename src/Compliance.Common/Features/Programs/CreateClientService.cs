using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.create", 2)]
public sealed record CreateClientService(Uuid TenantId, Uuid ProgramId, string Name, string Purpose,
    string OwnerReference) : IRequest<ClientServiceRegistration>, IProgramManagementRequest, ICallable;
