using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>One immutable version of a program's M0-D10 risk assessment method.</summary>
public sealed record RiskMethodVersionView(Uuid TenantId, Uuid ProgramId, Uuid MethodVersionId,
    long Version, string Kind, IReadOnlyList<string> LikelihoodScale,
    IReadOnlyList<string> ImpactScale, int? AppetiteThreshold, string ReassessmentInterval,
    ActorReference PublishedBy, DateTimeOffset PublishedAt);
