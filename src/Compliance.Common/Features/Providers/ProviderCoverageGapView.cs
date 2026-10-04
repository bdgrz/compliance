using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>A provider-specific uncovered assertion, distinct from any readiness gap it informs.</summary>
public sealed record ProviderCoverageGapView(Uuid TenantId, Uuid GapId, Uuid ProviderId,
    long ProviderRevision, long Revision, ProviderCoverageGapContent Content, string Status,
    ActorReference RecordedBy, DateTimeOffset RecordedAt,
    ProviderCoverageGapClosureView? Closure,
    IReadOnlyList<ProviderCoverageGapRiskAcceptanceView> RiskAcceptances)
{
    public bool Redacted { get; init; }
}

public sealed record ProviderCoverageGapClosureContent(string Resolution, string SourceKind,
    Uuid SourceId, long SourceRevision, string Rationale);

public sealed record ProviderCoverageGapClosureView(ProviderCoverageGapClosureContent Content,
    ActorReference Actor, DateTimeOffset ClosedAt)
{
    public long Revision { get; init; }
}

/// <summary>A link to the existing R1-07 decision; it does not close this provider source gap.</summary>
public sealed record ProviderCoverageGapRiskAcceptanceView(Uuid ProgramId, Uuid RiskId,
    Uuid AcceptanceId, DateTimeOffset ExpiresAt, ActorReference LinkedBy,
    DateTimeOffset LinkedAt)
{
    public long Revision { get; init; }
}

public sealed record ProviderCoverageGapRiskAcceptanceContent(Uuid ProgramId, Uuid RiskId,
    Uuid AcceptanceId);

public sealed record ProviderCoverageGapRegistration(Uuid GapId, long Revision);
