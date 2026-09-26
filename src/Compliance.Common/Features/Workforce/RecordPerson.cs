using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Records a workforce person manually. A person need not be a platform member.</summary>
[Discriminator("bdgrz.workforce.person.record", 1)]
public sealed record RecordPerson(Uuid TenantId, string DisplayName, string? WorkEmail = null)
    : IRequest<PersonRegistration>, IWorkforceRequest, ICallable;
