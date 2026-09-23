namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record PopulationDigest(long RowCount, int ChunkCount, string Sha256);
