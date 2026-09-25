using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

static class RiskDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<RiskDraftRevisionView> ByRiskRevision = new(
        "by_risk_revision", 1, static revision =>
            [revision.RiskId.ToString(),
                revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<RiskDraftRevisionView, string> Revisions = new(
        "risk_draft_history_revisions_v1",
        ComplianceCoreJsonContext.Default.RiskDraftRevisionView,
        static revision => RevisionKey(revision.RiskId, revision.Revision),
        static key => [key], [ByRiskRevision]);

    public static string RevisionKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
