using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Records the retire commitment intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.commitments.retire-commitment", 1)]
public sealed record RetireCommitment(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long ExpectedRevision, DateOnly EffectiveOn, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
