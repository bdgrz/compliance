namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>One planned storage chunk: a contiguous row range and the digest of its rows.</summary>
public sealed record PopulationStorageChunk(int Index, int Start, int Count, int EncodedBytes,
    string Sha256);
