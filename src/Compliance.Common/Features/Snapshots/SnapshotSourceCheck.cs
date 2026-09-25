namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record SnapshotSourceCheck(string Status, string ExpectedContentSha256,
    string? ObservedContentSha256);
