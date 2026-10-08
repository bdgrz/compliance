using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.directory-reevaluated", 1)]
public sealed record DirectoryIndependenceReevaluated(Uuid TenantId, long ExpectedSequence,
    DirectoryIndependenceReevaluationView Reevaluation) : DomainEvent;
