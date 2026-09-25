using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record SnapshotRegistration(Uuid SnapshotId, string ContentSha256);
