using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

static class AssuranceDirectorySchema
{
    public static readonly KvDirectoryIndex<AssuranceReportView> ReportsByProvider = new("reports_by_provider", 1,
        static view => [view.ProviderId.ToString(), view.ReportId.ToString()]);

    public static readonly KvDirectory<AssuranceReportView, Uuid> Reports = new("assurance_reports",
        ComplianceCoreJsonContext.Default.AssuranceReportView, static view => view.ReportId,
        static id => [id.ToString()], [ReportsByProvider]);

    public static readonly KvDirectory<AssuranceReportView, string> ReportRevisions =
        new("assurance_report_revisions", ComplianceCoreJsonContext.Default.AssuranceReportView,
            static view => ReportRevisionKey(view.ReportId, view.Revision), static key => [key], []);

    public static string ReportRevisionKey(Uuid reportId, long revision) =>
        $"{reportId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";

    public static readonly KvDirectoryIndex<ProviderReviewView> ReviewsByProvider = new("reviews_by_provider", 1,
        static view => [view.ProviderId.ToString(),
            view.Content.ReviewedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), view.ReviewId.ToString()]);

    public static readonly KvDirectory<ProviderReviewView, Uuid> Reviews = new("provider_reviews",
        ComplianceCoreJsonContext.Default.ProviderReviewView, static view => view.ReviewId,
        static id => [id.ToString()], [ReviewsByProvider]);

    public static readonly KvDirectoryIndex<ProviderCoverageGapView> CoverageGapsByProvider =
        new("coverage_gaps_by_provider", 1, static view =>
        [
            view.ProviderId.ToString(),
            view.RecordedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            view.GapId.ToString(),
        ]);

    public static readonly KvDirectory<ProviderCoverageGapView, Uuid> CoverageGaps =
        new("provider_coverage_gaps", ComplianceCoreJsonContext.Default.ProviderCoverageGapView,
            static view => view.GapId, static id => [id.ToString()], [CoverageGapsByProvider]);
}
