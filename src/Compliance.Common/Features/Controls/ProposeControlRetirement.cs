using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.retirement.propose", 1)]
public sealed record ProposeControlRetirement(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ExpectedApprovedVersionId, DateOnly EffectiveUntil, string Rationale)
    : IRequest<ControlRetirementRegistration>, IProgramScopedRequest, ICallable;
