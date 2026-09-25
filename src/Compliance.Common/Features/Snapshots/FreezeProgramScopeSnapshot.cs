using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.program_scope.freeze", 1)]
public sealed record FreezeProgramScopeSnapshot(Uuid TenantId, Uuid ProgramId,
    long ExpectedProgramRevision, Uuid BoundaryId, Uuid ApprovedBoundaryVersionId)
    : IRequest<SnapshotRegistration>, IProgramManagementRequest, ICallable;
