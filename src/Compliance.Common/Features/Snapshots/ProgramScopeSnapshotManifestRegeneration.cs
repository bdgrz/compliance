using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record ProgramScopeSnapshotManifestRegeneration(Uuid TenantId, Uuid SnapshotId,
    string CanonicalManifest, string ContentSha256);
