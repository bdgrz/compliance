using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.version.effective.get", 1)]
public sealed record GetEffectiveCommitmentVersion(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, DateOnly EffectiveOn)
    : IRequest<CommitmentVersionView>, IProgramScopedRequest, ICallable;
