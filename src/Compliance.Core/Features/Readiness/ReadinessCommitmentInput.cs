using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>One commitment's creation time and approved versions, so the rules can pick the version in force.</summary>
public sealed record ReadinessCommitmentInput(Uuid DraftId, string Identifier,
    DateTimeOffset CreatedAt, IReadOnlyList<CommitmentVersionView> Versions);
