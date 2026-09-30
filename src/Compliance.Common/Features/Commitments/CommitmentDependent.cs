using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentDependent(string Context, string RecordType, Uuid RecordId,
    string Relationship);
