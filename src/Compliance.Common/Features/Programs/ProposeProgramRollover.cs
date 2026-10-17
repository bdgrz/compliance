using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the propose program rollover intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.programs.propose-program-rollover", 1)]
public sealed record ProposeProgramRollover(Uuid TenantId, Uuid ProgramId, Uuid RolloverId, Uuid SourcePeriodId, DateOnly NextPeriodStart, DateOnly NextPeriodEnd, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
