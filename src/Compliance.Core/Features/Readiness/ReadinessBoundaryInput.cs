using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>One boundary's approved versions and decisions, so the rules can pick the version in force.</summary>
public sealed record ReadinessBoundaryInput(Uuid BoundaryId,
    IReadOnlyList<BoundaryVersionView> ApprovedVersions,
    IReadOnlyList<BoundaryDecisionView> Decisions);
