using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderCoverageGapTests
{
    [Fact]
    public void ShouldRetainProviderCoverageGapAndClosureGivenNewEvidence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var recordRequestId = Uuid.CreateVersion4();
        var closeRequestId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(tenantId);
        var content = Valid();
        var recorded = register.RecordCoverageGap(gapId, providerId, recordRequestId,
            4, content, Author, Now);
        var resolution = new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report",
            Uuid.CreateVersion4(), 1, "The new report covers the service and period.");

        // Act
        var closed = register.CloseCoverageGap(gapId, providerId, closeRequestId, 1,
            resolution, Author, Now.AddDays(1));
        var events = new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<ProviderAssuranceRegister>(
            new ProviderAssuranceRegister(tenantId)).Given(events).Aggregate;

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.True(closed.IsSuccess);
        var gap = Assert.IsType<ProviderCoverageGapView>(hydrated.CoverageGap(gapId));
        Assert.Equal(2, gap.Revision);
        Assert.Equal(4, gap.ProviderRevision);
        Assert.Equal("closed", gap.Status);
        Assert.Equal(content, gap.Content);
        Assert.Equal(resolution, gap.Closure!.Content);
        Assert.Equal(Author, gap.Closure.Actor);
        Assert.Equal(Now.AddDays(1), gap.Closure.ClosedAt);
    }

    [Fact]
    public void ShouldRejectClosureWithoutANewSourceRevisionGivenAnOpenGap()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(tenantId);
        var content = Valid();
        Assert.True(register.RecordCoverageGap(gapId, providerId, Uuid.CreateVersion4(),
            1, content, Author, Now).IsSuccess);
        var sameSource = new ProviderCoverageGapClosureContent("coverage_restored", content.SourceKind,
            content.SourceId, content.SourceRevision, "No new evidence.");

        // Act
        var result = register.CloseCoverageGap(gapId, providerId, Uuid.CreateVersion4(), 1,
            sameSource, Author, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal("open", register.CoverageGap(gapId)!.Status);
    }

    [Fact]
    public void ShouldRejectOlderRevisionOfSameSourceGivenOpenGap()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(tenantId);
        var content = Valid() with { SourceRevision = 3 };
        Assert.True(register.RecordCoverageGap(gapId, providerId, Uuid.CreateVersion4(),
            1, content, Author, Now).IsSuccess);
        var olderSource = new ProviderCoverageGapClosureContent("coverage_restored",
            content.SourceKind, content.SourceId, 2, "An earlier revision does not restore coverage.");

        // Act
        var result = register.CloseCoverageGap(gapId, providerId, Uuid.CreateVersion4(), 1,
            olderSource, Author, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal("open", register.CoverageGap(gapId)!.Status);
    }

    [Fact]
    public void ShouldLinkTimeBoundedRiskAcceptanceWithoutClosingGapGivenAcceptedExposure()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var acceptanceId = Uuid.CreateVersion4();
        var register = new ProviderAssuranceRegister(tenantId);
        Assert.True(register.RecordCoverageGap(gapId, providerId, Uuid.CreateVersion4(),
            1, Valid(), Author, Now).IsSuccess);
        var link = new ProviderCoverageGapRiskAcceptanceContent(programId, riskId, acceptanceId);

        // Act
        var result = register.LinkCoverageGapRiskAcceptance(gapId, providerId,
            Uuid.CreateVersion4(), 1, link, Now.AddMonths(2), Author, Now.AddDays(1));
        var events = new AggregateScenario<ProviderAssuranceRegister>(register).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<ProviderAssuranceRegister>(
            new ProviderAssuranceRegister(tenantId)).Given(events).Aggregate;

        // Assert
        Assert.True(result.IsSuccess);
        var gap = hydrated.CoverageGap(gapId)!;
        Assert.Equal("open", gap.Status);
        Assert.Equal(2, gap.Revision);
        var acceptance = Assert.Single(gap.RiskAcceptances!);
        Assert.Equal(programId, acceptance.ProgramId);
        Assert.Equal(riskId, acceptance.RiskId);
        Assert.Equal(acceptanceId, acceptance.AcceptanceId);
        Assert.Equal(Now.AddMonths(2), acceptance.ExpiresAt);
        Assert.Equal(Author, acceptance.LinkedBy);
        Assert.Equal(Now.AddDays(1), acceptance.LinkedAt);
    }

    [Fact]
    public void ShouldRedactRestrictedProviderCoverageGapDetailsGivenReadOnlyViewer()
    {
        // Arrange
        var content = Valid() with { Description = "Confidential report detail." };
        var closure = new ProviderCoverageGapClosureView(
            new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report",
                Uuid.CreateVersion4(), 3, "Confidential closure rationale."), Author, Now.AddDays(1))
        {
            Revision = 2,
        };
        var acceptance = new ProviderCoverageGapRiskAcceptanceView(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Now.AddMonths(2), Author, Now)
        {
            Revision = 2,
        };
        var gap = new ProviderCoverageGapView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, 2, content, "closed", Author, Now, closure, [acceptance]);

        // Act
        var redacted = ProviderCoverageGapRules.Redact(gap);

        // Assert
        Assert.True(redacted.Redacted);
        Assert.Equal("closed", redacted.Status);
        Assert.Equal("[redacted]", redacted.Content.Description);
        Assert.Equal("[redacted]", redacted.Closure!.Content.Rationale);
        Assert.Empty(redacted.RiskAcceptances);
    }

    [Fact]
    public void ShouldAcceptExactProviderCoverageGapGivenSupportedSource()
    {
        // Arrange
        var content = Valid();

        // Act
        var error = ProviderCoverageGapRules.InputError(content);

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("assertion")]
    [InlineData("reversed_period")]
    [InlineData("source_kind")]
    [InlineData("source_id")]
    [InlineData("source_revision")]
    [InlineData("description")]
    public void ShouldRejectProviderCoverageGapGivenIncompleteOrInvalidFact(string fault)
    {
        // Arrange
        var content = Valid();
        content = fault switch
        {
            "service" => content with { Service = " " },
            "assertion" => content with { Assertion = " " },
            "reversed_period" => content with { PeriodStart = new DateOnly(2026, 10, 2) },
            "source_kind" => content with { SourceKind = "assurance_report_or_anything" },
            "source_id" => content with { SourceId = Uuid.Empty },
            "source_revision" => content with { SourceRevision = 0 },
            _ => content with { Description = " " },
        };

        // Act
        var error = ProviderCoverageGapRules.InputError(content);

        // Assert
        Assert.NotNull(error);
    }

    static ProviderCoverageGapContent Valid() => new(
        Uuid.CreateVersion4(), "Payroll processing", "Availability assertion",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report",
        Uuid.CreateVersion4(), 2,
        "The SOC report does not cover the service for the full period.");
}
