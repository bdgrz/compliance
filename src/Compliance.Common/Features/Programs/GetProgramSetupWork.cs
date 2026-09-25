using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.setup-work.get", 1)]
public sealed record GetProgramSetupWork(Uuid TenantId, Uuid ProgramId,
    int? BoundaryLimit = null, string? BoundaryCursor = null,
    long? MinimumProgramRevision = null, Uuid? BoundaryId = null,
    long? MinimumBoundaryRevision = null)
    : IRequest<ProgramSetupWorkView>, ITenantAccessRequest, ICallable;
