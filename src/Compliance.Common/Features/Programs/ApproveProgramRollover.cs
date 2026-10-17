using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the approve program rollover intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.programs.approve-program-rollover", 1)]
public sealed record ApproveProgramRollover(Uuid TenantId, Uuid ProgramId, Uuid RolloverId, long Version, string Decision, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
