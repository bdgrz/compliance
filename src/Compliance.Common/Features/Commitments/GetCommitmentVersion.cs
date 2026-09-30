using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.version.get", 1)]
public sealed record GetCommitmentVersion(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Version)
    : IRequest<CommitmentVersionView>, IProgramScopedRequest, ICallable;
