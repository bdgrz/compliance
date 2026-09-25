using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.program_scope.verify", 1)]
public sealed record VerifyProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotVerification>, ITenantAccessRequest, ICallable;
