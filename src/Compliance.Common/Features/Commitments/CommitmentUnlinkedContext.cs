namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>A context that cannot reference commitments, with the reason it contributes no dependents.</summary>
public sealed record CommitmentUnlinkedContext(string Context, string Reason);
