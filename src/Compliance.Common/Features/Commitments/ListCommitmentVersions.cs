using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.version.list", 1)]
public sealed record ListCommitmentVersions(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentVersionView>>, IProgramScopedRequest, ICallable;
