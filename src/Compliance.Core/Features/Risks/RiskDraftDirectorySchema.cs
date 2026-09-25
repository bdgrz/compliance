using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

static class RiskDraftDirectorySchema
{
    public static readonly KvDirectoryIndex<RiskDraftView> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static risk => [risk.ProgramId.ToString(), risk.Identifier]);

    public static readonly KvDirectory<RiskDraftView, Uuid> Risks = new(
        "risk_drafts", ComplianceCoreJsonContext.Default.RiskDraftView,
        static risk => risk.RiskId,
        static riskId => [riskId.ToString()], [ByProgramIdentifier]);

    public static readonly KvDirectory<RiskDraftRevisionView, string> Revisions = new(
        "risk_draft_revisions", ComplianceCoreJsonContext.Default.RiskDraftRevisionView,
        static revision => RevisionKey(revision.RiskId, revision.Revision),
        static key => [key], []);

    public static string RevisionKey(Uuid riskId, long revision) =>
        $"{riskId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
