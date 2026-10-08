namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>A denial-only evaluation; ordinary membership, grants and campaign audience checks remain required.</summary>
public sealed record PolicyAcknowledgementRecorderEvaluation(PolicyAcknowledgementPersonSnapshot Person,
    bool IsSelf, bool CanRecordProxy);
