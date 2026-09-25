using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.create", 1)]
public sealed record CreateRiskDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    string Title, string Scenario, string PotentialEffect, string? SourceNote = null)
    : IRequest<RiskRegistration>, IProgramManagementRequest, ICallable;
