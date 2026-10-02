using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceCoverageTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProviderId = Uuid.CreateVersion4();

    [Theory]
    [InlineData("2026-06-30", "current", "covers_as_of")]
    [InlineData("2025-07-01", "current", "covers_as_of")]
    [InlineData("2025-06-30", "current", "not_yet_effective")]
    [InlineData("2026-07-01", "current", "uncovered_after_period")]
    [InlineData("2027-06-30", "current", "uncovered_after_period")]
    [InlineData("2027-07-01", "stale", "uncovered_after_period")]
    public void ShouldClassifyReportGivenAsOfWithoutBridgeLetter(string asOf, string currency, string coverage)
    {
        // Arrange
        var report = ReportView(Report());

        // Act
        var result = ProviderAssuranceCoverage.Evaluate(report, DateOnly.Parse(asOf, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        Assert.Equal(currency, result.Currency);
        Assert.Equal(coverage, result.PeriodCoverage);
        Assert.Equal(asOf == "2026-06-30" || asOf == "2025-07-01", result.Complete);
    }

    [Theory]
    [InlineData("2026-07-31", "uncovered_after_period")]
    [InlineData("2026-08-01", "bridged_only")]
    [InlineData("2026-12-31", "bridged_only")]
    [InlineData("2027-01-01", "bridge_expired")]
    public void ShouldNeverCompleteCoverageGivenBridgeLetter(string asOf, string coverage)
    {
        // Arrange
        var bridged = Report() with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)) };

        // Act
        var result = ProviderAssuranceCoverage.Evaluate(ReportView(bridged), DateOnly.Parse(asOf, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        Assert.Equal(coverage, result.PeriodCoverage);
        Assert.False(result.Complete);
        Assert.Contains(coverage, result.IncompleteReasons);
        Assert.Equal(new DateOnly(2026, 12, 31), result.BridgeCoversThrough);
    }

    [Fact]
    public void ShouldNotCompleteCoverageGivenStaleReportEvenWithUnexpiredBridgeLetter()
    {
        // Arrange
        var bridged = Report() with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1), new DateOnly(2028, 12, 31)) };

        // Act
        var result = ProviderAssuranceCoverage.Evaluate(ReportView(bridged), new DateOnly(2027, 8, 1));

        // Assert
        Assert.Equal("stale", result.Currency);
        Assert.Equal("bridged_only", result.PeriodCoverage);
        Assert.False(result.Complete);
        Assert.Contains("report_stale", result.IncompleteReasons);
    }

    [Theory]
    [InlineData("qualified", 0, 0, "opinion_qualified")]
    [InlineData("adverse", 0, 0, "opinion_adverse")]
    [InlineData("disclaimer", 0, 0, "opinion_disclaimer")]
    [InlineData("unqualified", 1, 0, "exceptions_noted")]
    [InlineData("unqualified", 0, 1, "coverage_gaps_declared")]
    public void ShouldNotCompleteCoverageGivenOpinionExceptionOrDeclaredGap(string opinion, int exceptions, int gaps, string reason)
    {
        // Arrange
        var content = Report() with
        {
            Opinion = opinion,
            Exceptions = Enumerable.Range(0, exceptions).Select(_ => new AssuranceExceptionNote(null, "Noted")).ToArray(),
            CoverageGaps = Enumerable.Repeat("Subservice not covered", gaps).ToArray(),
        };

        // Act
        var result = ProviderAssuranceCoverage.Evaluate(ReportView(content), new DateOnly(2026, 6, 30));

        // Assert
        Assert.False(result.Complete);
        Assert.Equal([reason], result.IncompleteReasons);
    }

    [Fact]
    public void ShouldReportNotRequiredAndUnresolvedGivenMaterialityNotMaterialOrMissing()
    {
        // Arrange
        var asOf = new DateOnly(2026, 10, 2);

        // Act
        var notMaterial = ProviderAssuranceCoverage.Evaluate(Provider(new ProviderContent("A", "S", "not_material")), [], [], asOf);
        var unresolved = ProviderAssuranceCoverage.Evaluate(Provider(new ProviderContent("A", "S")), [], [], asOf);

        // Assert
        Assert.Equal("not_required", notMaterial.Status);
        Assert.Equal("unresolved_materiality", unresolved.Status);
        Assert.False(notMaterial.Complete);
        Assert.False(unresolved.Complete);
    }

    [Fact]
    public void ShouldCascadeStatusGivenMaterialProviderReviewHistory()
    {
        // Arrange
        var report = ReportView(Report());
        var review = ReviewView(report, "acceptable");
        var provider = Provider(MaterialProvider());

        // Act
        var none = Evaluate(provider, [report], [], new DateOnly(2026, 10, 2));
        var current = Evaluate(provider, [report], [review], new DateOnly(2026, 10, 2));
        var later = Evaluate(provider, [report], [review], new DateOnly(2027, 2, 1));
        var overdue = Evaluate(provider, [report], [review], new DateOnly(2027, 9, 2));
        var beforeReview = Evaluate(provider, [report], [review], new DateOnly(2026, 8, 31));
        var stale = Evaluate(provider, [report], [review with { Content = review.Content with { NextReviewDue = new DateOnly(2028, 9, 1) } }], new DateOnly(2027, 8, 1));
        var rejected = Evaluate(provider, [report], [ReviewView(report, "not_acceptable")], new DateOnly(2026, 10, 2));
        var unrecorded = Evaluate(provider, [], [review], new DateOnly(2026, 10, 2));
        var withExceptions = Evaluate(provider, [report], [ReviewView(report, "acceptable_with_exceptions")], new DateOnly(2026, 10, 2));

        // Assert
        Assert.Equal("no_review", none.Status);
        Assert.Equal("current", later.Status);
        Assert.Equal(["uncovered_after_period"], later.Reasons);
        Assert.Equal("review_overdue", overdue.Status);
        Assert.Equal("no_review", beforeReview.Status);
        Assert.Equal("evidence_stale", stale.Status);
        Assert.Equal("not_acceptable", rejected.Status);
        Assert.Equal("evidence_unrecorded", unrecorded.Status);
        Assert.Equal("current", current.Status);
        Assert.Equal(review.ReviewId, current.LatestReviewId);
        Assert.Equal(["uncovered_after_period"], current.Reasons);
        Assert.False(current.Complete);
        Assert.Equal("current", withExceptions.Status);
        Assert.Contains("review_has_exceptions", withExceptions.Reasons);
        Assert.False(withExceptions.Complete);
    }

    [Fact]
    public void ShouldCompleteGivenAcceptableReviewOfCompleteReportInPeriod()
    {
        // Arrange
        var report = ReportView(Report());
        var review = ReviewView(report, "acceptable") with { Content = Review(report.ReportId) with { ReviewedAt = new DateOnly(2026, 6, 30), NextReviewDue = new DateOnly(2027, 6, 30) } };

        // Act
        var result = Evaluate(Provider(MaterialProvider()), [report], [review], new DateOnly(2026, 6, 30));

        // Assert
        Assert.Equal("current", result.Status);
        Assert.True(result.Complete);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void ShouldFlagReportRevisedSinceReviewGivenNewerReportRevision()
    {
        // Arrange
        var report = ReportView(Report());
        var review = ReviewView(report, "acceptable");

        // Act
        var result = Evaluate(Provider(MaterialProvider()), [report with { Revision = 2 }], [review], new DateOnly(2026, 10, 2));

        // Assert
        Assert.Equal("current", result.Status);
        Assert.Contains("report_revised_since_review", result.Reasons);
    }

    static ProviderAssuranceCoverageView Evaluate(ProviderView provider, AssuranceReportView[] reports,
        ProviderReviewView[] reviews, DateOnly asOf) => ProviderAssuranceCoverage.Evaluate(provider, reports, reviews, asOf);

    static ProviderView Provider(ProviderContent content) => new(TenantId, ProviderId, 1, content, "manual", "active", [], Author, Now);

    static AssuranceReportView ReportView(AssuranceReportContent content) => new(TenantId, Uuid.CreateVersion4(), ProviderId, 1, content, 1,
        content.Exceptions?.Count ?? 0, content.ComplementaryControls?.Count ?? 0, content.CoverageGaps?.Count ?? 0, Author, Now);

    static ProviderReviewView ReviewView(AssuranceReportView report, string conclusion) => new(TenantId, Uuid.CreateVersion4(), ProviderId,
        Review(report.ReportId, conclusion), 1, report.Revision, Author, Now);
}
