using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

static class RiskEvaluationDirectorySchema
{
    public static readonly KvDirectory<RiskEvaluationView, Uuid> Evaluations = new(
        "risk_evaluations_v1", ComplianceCoreJsonContext.Default.RiskEvaluationView,
        static evaluation => evaluation.RiskId,
        static riskId => [riskId.ToString()], []);

    public static readonly KvDirectoryIndex<RiskEvaluationHistoryEntryView> ByRiskRevision = new(
        "by_risk_revision", 1, static entry =>
            [entry.RiskId.ToString(), entry.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<RiskEvaluationHistoryEntryView, string> History = new(
        "risk_evaluation_history_v1",
        ComplianceCoreJsonContext.Default.RiskEvaluationHistoryEntryView,
        static entry => HistoryKey(entry.RiskId, entry.Revision),
        static key => [key], [ByRiskRevision]);

    public static string HistoryKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
