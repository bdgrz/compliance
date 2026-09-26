using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.revise", 1)]
public sealed record RevisePerson(Uuid TenantId, Uuid PersonId, long ExpectedRevision,
    string DisplayName, string? WorkEmail = null)
    : IRequest, IWorkforceRequest, ICallable;
