using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceRegisterTests
{
    [Fact]
    public void ShouldRetainOriginalDecisionGivenHydratedRetryAfterReportRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var revisionId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(tenantId);
        Assert.True(register.RecordReport(reportId, providerId, requestId, Report(), 1, Author, Now).IsSuccess);
        var revised = Report() with { Issuer = "Renamed Assurance LLP" };
        Assert.True(register.ReviseReport(reportId, providerId, revisionId, 1, revised, 1, Author, Now.AddMinutes(1)).IsSuccess);
        var retained = new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<ProviderAssuranceRegister>(new ProviderAssuranceRegister(tenantId)).Given(retained).Aggregate;

        // Act
        var retry = hydrated.RecordReport(reportId, providerId, requestId, Report(), 9, Author, Now.AddDays(1));
        var revisionRetry = hydrated.ReviseReport(reportId, providerId, revisionId, 1, revised, 9, Author, Now.AddDays(1));
        var conflicting = hydrated.RecordReport(reportId, providerId, requestId, Report() with { Issuer = "Other" }, 1, Author, Now);

        // Assert
        Assert.Equal(1, retry.Value!.Revision);
        Assert.Equal(2, revisionRetry.Value!.Revision);
        Assert.Equal(RequestErrorKind.Conflict, conflicting.Error!.Kind);
        Assert.Empty(new AggregateScenario<ProviderAssuranceRegister>(hydrated).PendingEvents);
        Assert.Equal("Renamed Assurance LLP", hydrated.Report(reportId)!.Content.Issuer);
        Assert.Equal(2, hydrated.Report(reportId)!.Revision);
        Assert.Equal(["Example Assurance LLP"], [Assert.IsType<AssuranceReportRecorded>(retained[0]).Content.Issuer]);
    }

    [Fact]
    public void ShouldRejectStaleRevisionAndForeignProviderGivenExistingReport()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        Assert.True(register.RecordReport(reportId, providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now).IsSuccess);

        // Act
        var stale = register.ReviseReport(reportId, providerId, Uuid.CreateVersion4(), 0, Report(), 1, Author, Now);
        var otherProvider = register.ReviseReport(reportId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Report(), 1, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Contains("Current revision: 1", stale.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.NotFound, otherProvider.Error!.Kind);
        Assert.Single(new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents);
    }

    [Theory]
    [InlineData("kind", "soc1")]
    [InlineData("issuer", "")]
    [InlineData("scope", "  ")]
    [InlineData("opinion", "clean")]
    [InlineData("opinion_missing", "")]
    [InlineData("opinion_source_missing", "")]
    [InlineData("type1_period", "")]
    [InlineData("type2_period_missing", "")]
    [InlineData("period_reversed", "")]
    [InlineData("iso_opinion", "")]
    [InlineData("bridge_overlaps_period", "")]
    [InlineData("bridge_reversed", "")]
    [InlineData("citation_unclassified", "")]
    [InlineData("citation_unknown_class", "secret")]
    [InlineData("empty_exception", "")]
    [InlineData("too_many_gaps", "")]
    public void ShouldRejectReportGivenInvalidFact(string fault, string value)
    {
        // Arrange
        var good = Report();
        var content = fault switch
        {
            "kind" => good with { ReportKind = value },
            "issuer" => good with { Issuer = value },
            "scope" => good with { Scope = value },
            "opinion" => good with { Opinion = value },
            "opinion_missing" => good with { Opinion = null, OpinionSource = null },
            "opinion_source_missing" => good with { OpinionSource = null },
            "type1_period" => good with { ReportKind = "soc2_type1" },
            "type2_period_missing" => good with { PeriodStart = null },
            "period_reversed" => good with { PeriodStart = new DateOnly(2026, 7, 1) },
            "iso_opinion" => good with { ReportKind = "iso27001", PeriodStart = null },
            "bridge_overlaps_period" => good with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 1), new DateOnly(2026, 6, 30), new DateOnly(2026, 12, 31)) },
            "bridge_reversed" => good with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 31)) },
            "citation_unclassified" => good with { Citation = Citation(value) },
            "citation_unknown_class" => good with { Citation = Citation(value) },
            "empty_exception" => good with { Exceptions = [new AssuranceExceptionNote(null, " ")] },
            _ => good with { CoverageGaps = Enumerable.Repeat("gap", 51).ToArray() },
        };
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());

        // Act
        var result = register.RecordReport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), content, 1, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldAcceptRestrictedCitationAndRetainNoOpinionGivenIsoCertificate()
    {
        // Arrange
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        var iso = new AssuranceReportContent("iso27001", "Certification Body", "ISMS for hosting", new DateOnly(2026, 3, 1),
            Citation: Citation("restricted"));

        // Act
        var result = register.RecordReport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), iso, 1, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldRejectOversizedReportGivenBoundedFieldsAtTheirLimits()
    {
        // Arrange
        var exceptions = Enumerable.Range(0, 50).Select(_ => new AssuranceExceptionNote(new string('r', 200), new string('d', 2000))).ToArray();
        var content = Report() with
        {
            Exceptions = exceptions,
            CoverageGaps = Enumerable.Repeat(new string('g', 2000), 50).ToArray(),
            ComplementaryControls = Enumerable.Repeat(new string('c', 2000), 50).ToArray(),
        };
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());

        // Act
        var result = register.RecordReport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), content, 1, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("payload", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldReturnValidationGivenNullNestedEntries()
    {
        // Arrange
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());

        // Act
        var report = register.RecordReport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Report() with { Exceptions = [null!] }, 1, Author, Now);
        var review = register.RecordReview(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Review() with { Exceptions = [null!] }, 1, "material", Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, report.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, review.Error!.Kind);
    }

    [Fact]
    public void ShouldRecordImmutableReviewGivenSameProviderReportOfSameKind()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        Assert.True(register.RecordReport(reportId, providerId, Uuid.CreateVersion4(), Report(), 3, Author, Now).IsSuccess);
        var review = Review(reportId);

        // Act
        var recorded = register.RecordReview(requestId, providerId, requestId, review, 3, "material", Author, Now);
        var retry = register.RecordReview(requestId, providerId, requestId, review, 4, "not_material", Author, Now.AddDays(1));
        var changed = register.RecordReview(requestId, providerId, requestId, review with { Rationale = "Different" }, 3, "material", Author, Now);

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.Equal(requestId, recorded.Value!.ReviewId);
        Assert.Equal(requestId, retry.Value!.ReviewId);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        var stored = Assert.Single(register.Reviews(providerId));
        Assert.Equal(1, stored.AssuranceReportRevision);
        Assert.Equal(3, stored.ProviderRevision);
        Assert.Equal(Author, stored.ReviewedBy);
    }

    [Theory]
    [InlineData("other_provider", RequestErrorKind.NotFound)]
    [InlineData("unknown_report", RequestErrorKind.NotFound)]
    [InlineData("kind_mismatch", RequestErrorKind.Validation)]
    [InlineData("before_period_end", RequestErrorKind.Validation)]
    [InlineData("stale_report_acceptable", RequestErrorKind.Validation)]
    [InlineData("future_review", RequestErrorKind.Validation)]
    [InlineData("cadence_over_one_year", RequestErrorKind.Validation)]
    [InlineData("next_not_after_review", RequestErrorKind.Validation)]
    [InlineData("no_evidence", RequestErrorKind.Validation)]
    [InlineData("exceptions_with_acceptable", RequestErrorKind.Validation)]
    [InlineData("missing_exceptions", RequestErrorKind.Validation)]
    [InlineData("unknown_conclusion", RequestErrorKind.Validation)]
    public void ShouldRejectReviewGivenUnsupportedConclusion(string fault, RequestErrorKind expected)
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        Assert.True(register.RecordReport(reportId, providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now).IsSuccess);
        var good = Review(reportId);
        var reviewedProvider = providerId;
        var content = fault switch
        {
            "other_provider" => good,
            "unknown_report" => good with { AssuranceReportId = Uuid.CreateVersion4() },
            "kind_mismatch" => good with { EvidenceKind = "iso27001" },
            "before_period_end" => good with { ReviewedAt = new DateOnly(2026, 6, 1), NextReviewDue = new DateOnly(2027, 6, 1) },
            "stale_report_acceptable" => good with { ReviewedAt = new DateOnly(2027, 7, 1), NextReviewDue = new DateOnly(2027, 8, 1) },
            "future_review" => good with { ReviewedAt = new DateOnly(2026, 10, 3), NextReviewDue = new DateOnly(2027, 10, 3) },
            "cadence_over_one_year" => good with { NextReviewDue = new DateOnly(2027, 9, 2) },
            "next_not_after_review" => good with { NextReviewDue = good.ReviewedAt },
            "no_evidence" => good with { AssuranceReportId = null },
            "exceptions_with_acceptable" => good with { Exceptions = ["Unexpected"] },
            "missing_exceptions" => good with { Conclusion = "acceptable_with_exceptions" },
            _ => good with { Conclusion = "approved" },
        };
        if (fault == "other_provider")
            reviewedProvider = Uuid.CreateVersion4();

        // Act
        var result = register.RecordReview(Uuid.CreateVersion4(), reviewedProvider, Uuid.CreateVersion4(), content, 1, "material", Author,
            fault == "stale_report_acceptable" ? Now.AddYears(1) : Now);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(register.Reviews(providerId));
    }

    [Fact]
    public void ShouldAllowStaleReportGivenNotAcceptableConclusionAndLongerCadenceGivenNotMaterialProvider()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        Assert.True(register.RecordReport(reportId, providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now).IsSuccess);
        var stale = Review(reportId, "not_acceptable") with { ReviewedAt = new DateOnly(2027, 7, 1), NextReviewDue = new DateOnly(2027, 8, 1) };
        var longCadence = Review(reportId) with { NextReviewDue = new DateOnly(2029, 9, 1) };

        // Act
        var notAcceptable = register.RecordReview(Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), stale, 1, "material", Author, Now.AddYears(1));
        var capped = register.RecordReview(Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), longCadence, 1, null, Author, Now);
        var notMaterial = register.RecordReview(Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), longCadence, 1, "not_material", Author, Now);

        // Assert
        Assert.True(notAcceptable.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, capped.Error!.Kind);
        Assert.True(notMaterial.IsSuccess);
    }

    [Fact]
    public void ShouldBoundRecordsPerProviderGivenRetainedLimit()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(Uuid.CreateVersion4());
        for (var index = 0; index < ProviderAssuranceRegister.MaximumRecordsPerProvider; index++)
            Assert.True(register.RecordReport(Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now).IsSuccess);

        // Act
        var overflow = register.RecordReport(Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now);
        var otherProvider = register.RecordReport(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Report(), 1, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, overflow.Error!.Kind);
        Assert.True(otherProvider.IsSuccess);
    }
}
