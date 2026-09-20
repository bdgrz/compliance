using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramSetupWorkItem(string Code, string Detail,
    string SourceType, Uuid? SourceId);

public sealed record ProgramSetupWorkView(Uuid TenantId, Uuid ProgramId,
    long ProgramRevision, IReadOnlyList<ProgramSetupWorkItem> Items,
    string? NextBoundaryCursor);

[Discriminator("bdgrz.program.setup-work.get", 1)]
public sealed record GetProgramSetupWork(Uuid TenantId, Uuid ProgramId,
    int? BoundaryLimit = null, string? BoundaryCursor = null,
    long? MinimumProgramRevision = null, Uuid? BoundaryId = null,
    long? MinimumBoundaryRevision = null)
    : IRequest<ProgramSetupWorkView>, ITenantAccessRequest, ICallable;
