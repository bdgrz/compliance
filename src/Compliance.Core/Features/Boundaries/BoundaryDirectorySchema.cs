using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

static class BoundaryDirectorySchema
{
    public static readonly KvDirectoryIndex<BoundaryVersionView> VersionsByBoundary = new(
        "by_boundary", 1, static version =>
            [version.BoundaryId.ToString(),
                version.EffectiveFrom!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<BoundaryVersionView, Uuid> Versions = new(
        "boundary_versions", ComplianceCoreJsonContext.Default.BoundaryVersionView,
        static version => version.VersionId,
        static versionId => [versionId.ToString()], [VersionsByBoundary]);

    public static readonly KvDirectoryIndex<BoundaryDecisionView> DecisionsByBoundary = new(
        "by_boundary", 1, static decision =>
            [decision.BoundaryId.ToString(),
                decision.DecidedAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                decision.DecisionId.ToString()]);

    public static readonly KvDirectory<BoundaryDecisionView, Uuid> Decisions = new(
        "boundary_decisions", ComplianceCoreJsonContext.Default.BoundaryDecisionView,
        static decision => decision.DecisionId,
        static decisionId => [decisionId.ToString()], [DecisionsByBoundary]);

    public static readonly KvDirectoryIndex<BoundaryView> ByProgram = new(
        "by_program", 1, static boundary =>
            [boundary.ProgramId.ToString(), boundary.BoundaryId.ToString()]);

    public static readonly KvDirectory<BoundaryView, Uuid> Directory = new(
        "boundaries", ComplianceCoreJsonContext.Default.BoundaryView,
        static boundary => boundary.BoundaryId,
        static boundaryId => [boundaryId.ToString()], [ByProgram]);
}
