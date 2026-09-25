using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.program.list", 1)]
public sealed record ListProgramSnapshots(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<SnapshotView>>, ITenantAccessRequest, ICallable;
