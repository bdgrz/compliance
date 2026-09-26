using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.get", 1)]
public sealed record GetPerson(Uuid TenantId, Uuid PersonId, long? MinimumRevision = null)
    : IRequest<PersonView>, IWorkforceRequest, ICallable;
