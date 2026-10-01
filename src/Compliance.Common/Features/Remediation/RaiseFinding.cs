using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Records a finding from a named source with its wording preserved.</summary>
[Discriminator("bdgrz.finding.raise", 1)]
public sealed record RaiseFinding(Uuid TenantId, Uuid ProgramId, FindingSource Source,
    string Title, string Description, string Severity, string AffectedScope,
    Uuid OwnerMemberId, DateOnly DueOn, IReadOnlyList<FindingLink>? Links = null)
    : IRequest<FindingRegistration>, IProgramScopedRequest, ICallable;
