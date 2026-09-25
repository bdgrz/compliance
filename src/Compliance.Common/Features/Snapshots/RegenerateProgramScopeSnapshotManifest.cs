using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

[Discriminator("bdgrz.snapshot.program_scope.manifest_regenerate", 1)]
public sealed record RegenerateProgramScopeSnapshotManifest(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotManifestRegeneration>, ITenantAccessRequest, ICallable;
