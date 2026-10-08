using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Tests.Features.ControlMappings;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessAssessmentHandlerTests
{
    static readonly Uuid Edition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-current");

    [Fact]
    public async Task ShouldRecordExplainableFindingsGivenMappedAndUnmappedCriteria()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.MapAsync("CC6.1", fixture.ReviewedAt);

        // Act
        var assessment = await fixture.RunAsync(0);

        // Assert
        Assert.Equal(ReadinessRules.Version, assessment.RuleVersion);
        Assert.Equal(Edition, assessment.EditionId);
        Assert.DoesNotContain(assessment.Findings, f => f.CriterionIdentifier.StartsWith("bdgrz:",
            StringComparison.Ordinal));
        var met = assessment.Findings.Where(f => f.CriterionIdentifier == "CC6.1").ToArray();
        Assert.All(met, f => Assert.Equal("rule_met", f.Outcome));
        Assert.Contains(met.SelectMany(f => f.Sources), s => s.Kind == "control_version" &&
            s.Id == fixture.ControlVersionId);
        var unmapped = Assert.Single(assessment.Findings, f => f.CriterionIdentifier == "CC6.2" &&
            f.RuleId == ReadinessRules.CriterionMapped);
        Assert.Equal("gap", unmapped.Outcome);
        Assert.Contains(assessment.Gaps, g => g.GapId == unmapped.GapId);
        Assert.Contains(assessment.Inputs, i => i.Family == "risks" && i.Status == "assessed");
        Assert.Contains(assessment.Gaps, g => g.Kind == "no_risks" && g.Subject == "risks");
        Assert.Contains(assessment.Gaps, g => g.Kind == "input_not_assessed" &&
            g.Subject == "evidence");
        Assert.Null(assessment.Decision);
        Assert.Equal(64, assessment.InputFingerprint.Length);
    }

    [Fact]
    public async Task ShouldExcludeLaterMappingGivenAssessmentAsOfBeforeReview()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.MapAsync("CC6.1", fixture.ReviewedAt);

        // Act
        var earlier = await fixture.RunAsync(0, fixture.ReviewedAt.AddMinutes(-5));
        var later = await fixture.RunAsync(1, fixture.ReviewedAt.AddMinutes(5));

        // Assert
        Assert.Equal("gap", Assert.Single(earlier.Findings, f => f.CriterionIdentifier ==
            "CC6.1" && f.RuleId == ReadinessRules.CriterionMapped).Outcome);
        Assert.Equal("rule_met", Assert.Single(later.Findings, f => f.CriterionIdentifier ==
            "CC6.1" && f.RuleId == ReadinessRules.CriterionMapped).Outcome);
        Assert.NotEqual(earlier.InputFingerprint, later.InputFingerprint);
        var gapIds = earlier.Gaps.Where(g => g.Subject == "CC6.2").Select(g => g.GapId);
        Assert.Equal(gapIds, later.Gaps.Where(g => g.Subject == "CC6.2").Select(g => g.GapId));
    }

    [Fact]
    public async Task ShouldRecordGapGivenMappedControlWithoutEffectiveVersion()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(DateOnly.FromDateTime(DateTime.UtcNow)
            .AddDays(30));
        await fixture.MapAsync("CC6.1", fixture.ReviewedAt);

        // Act
        var assessment = await fixture.RunAsync(0);

        // Assert
        var finding = Assert.Single(assessment.Findings, f => f.CriterionIdentifier == "CC6.1" &&
            f.RuleId == ReadinessRules.MappedControlEffective);
        Assert.Equal("gap", finding.Outcome);
        Assert.Contains(finding.Sources, s => s.Kind == "control_criterion_mapping");
    }

    [Fact]
    public async Task ShouldRecordAcknowledgedInputGapGivenProgramWithoutCriteriaEdition()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(selectEdition: false);

        // Act
        var assessment = await fixture.RunAsync(0);

        // Assert
        Assert.Null(assessment.EditionId);
        Assert.Empty(assessment.Findings);
        Assert.Contains(assessment.Gaps, g => g.Kind == "input_not_assessed" &&
            g.Subject == "criteria_catalog");
    }

    [Fact]
    public async Task ShouldRejectRunGivenFutureAsOfOrStaleRevision()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.RunAsync(0);

        await fixture.Scenario(fixture.RunnerUserId)
            // Act
            .When(new RunReadinessAssessment(fixture.TenantId, fixture.ProgramId, 0))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new RunReadinessAssessment(fixture.TenantId, fixture.ProgramId, 1,
                DateTimeOffset.UtcNow.AddDays(1)))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectDecisionGivenRunnerDecidesOwnAssessment()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);

        // Act
        var result = await PersonalReadinessClosureTransportTests.DecideHttpAsync(fixture.Provider, fixture.RunnerUserId, new DecideReadiness(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, 1, "do_not_proceed", "Too many gaps."), RequestErrorKind.Forbidden);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRequireOwnedGapPlanGivenProceedDecision()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var decide = new DecideReadiness(fixture.TenantId, fixture.ProgramId,
            assessment.AssessmentId, 1, "proceed", "Gaps have owners and dates.");
        await PersonalReadinessClosureTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, decide, RequestErrorKind.Conflict);
        var revision = 1L;
        foreach (var gap in assessment.Gaps)
        {
            await fixture.Scenario(fixture.RunnerUserId)
                .When(new PlanReadinessGap(fixture.TenantId, fixture.ProgramId, gap.GapId,
                    revision, fixture.OwnerMemberId, new DateOnly(2027, 3, 31), "Close it."))
                .ExpectSuccess();
            revision++;
        }

        // Act
        var decision = await PersonalReadinessClosureTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, decide with { ExpectedRevision = revision });

        // Assert
        Assert.Equal("proceed", decision.Value.Outcome);
        var read = await fixture.GetAsync(assessment.AssessmentId);
        Assert.Equal("proceed", read.Decision!.Outcome);
        Assert.All(read.Gaps, g => Assert.Equal(fixture.OwnerMemberId, g.Plan!.OwnerMemberId));
        var unplanned = await fixture.QueryAsync<ListReadinessGaps, Page<ReadinessGapView>>(
            new ListReadinessGaps(fixture.TenantId, fixture.ProgramId, assessment.AssessmentId,
                "unplanned"));
        Assert.Empty(unplanned.Items);
        await PersonalReadinessClosureTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, decide with { ExpectedRevision = revision + 1, Outcome = "do_not_proceed" }, RequestErrorKind.Conflict);
        var list = await fixture.QueryAsync<ListReadinessAssessments,
            Page<ReadinessAssessmentSummaryView>>(new ListReadinessAssessments(fixture.TenantId,
            fixture.ProgramId));
        Assert.Equal("proceed", Assert.Single(list.Items).DecisionOutcome);
    }

    [Fact]
    public async Task ShouldRejectGapPlanGivenUnknownGap()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.RunAsync(0);

        await fixture.Scenario(fixture.RunnerUserId)
            // Act
            .When(new PlanReadinessGap(fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4(), 1, fixture.OwnerMemberId, new DateOnly(2027, 1, 1),
                "Plan."))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenAssessmentFromAnotherTenantOrProgram()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);

        await fixture.Scenario(fixture.DeciderUserId)
            // Act
            .When(new GetReadinessAssessment(fixture.TenantId, Uuid.CreateVersion4(),
                assessment.AssessmentId))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.DeciderUserId)
            .When(new GetReadinessAssessment(Uuid.CreateVersion4(), fixture.ProgramId,
                assessment.AssessmentId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldRecordTypeIEntryWithFrozenAcknowledgedItemsGivenPlannedGapsAndProceed()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var revision = await fixture.PlanAllAndProceedAsync(assessment);
        var gapIds = assessment.Gaps.Select(static g => g.GapId).ToArray();

        // Act
        var decision = await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, revision, "approve_with_exceptions",
                "Gaps are owned and acknowledged.", gapIds));

        // Assert
        var view = decision.Value;
        Assert.Equal(assessment.InputFingerprint, view.InputFingerprint);
        Assert.Equal(ReadinessRules.Version, view.RuleVersion);
        Assert.Equal(gapIds.Length, view.UnresolvedItems.Count);
        Assert.All(view.UnresolvedItems, item => Assert.True(item.Acknowledged));
        Assert.All(view.UnresolvedItems, item => Assert.Equal(fixture.OwnerMemberId,
            item.OwnerMemberId));
        Assert.Contains(view.UnresolvedItems, item => item.Subject == "evidence");
        Assert.Contains("not an auditor opinion", view.Notice, StringComparison.Ordinal);
        var read = await fixture.GetAsync(assessment.AssessmentId);
        Assert.Equal(view.DecisionId, read.TypeIEntryDecision!.DecisionId);
        var list = await fixture.QueryAsync<ListTypeIEntryDecisions,
            Page<TypeIEntryDecisionView>>(new ListTypeIEntryDecisions(fixture.TenantId,
            fixture.ProgramId));
        Assert.Single(list.Items);
        var later = await fixture.RunAsync(revision + 1);
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId, later.AssessmentId,
                revision + 2, "defer", "Second try."), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRejectTypeIEntryApprovalGivenUnacknowledgedGapOrPlainApprove()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var revision = await fixture.PlanAllAndProceedAsync(assessment);
        var partial = assessment.Gaps.Skip(1).Select(static g => g.GapId).ToArray();

        // Act
        // Assert
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, revision, "approve_with_exceptions", "Partial.",
                partial), RequestErrorKind.Conflict);
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, revision, "approve", "Ignore gaps."), RequestErrorKind.Conflict);
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, revision, "approve_with_exceptions", "Unknown.",
                [Uuid.CreateVersion4()]), RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectTypeIEntryGivenRunnerSignsOrNoProceedDecision()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var all = assessment.Gaps.Select(static g => g.GapId).ToArray();

        // Act
        // Assert
        var denied = await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.RunnerUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, 1, "defer", "Self sign."), RequestErrorKind.Forbidden);
        Assert.Equal("The member who ran an assessment cannot sign its Type I entry decision.", denied.Error!.Message);
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, 1, "approve_with_exceptions", "No proceed.", all), RequestErrorKind.Conflict);
        var deferred = await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, 1, "defer", "Not ready yet."));
        Assert.Equal("defer", deferred.Value.Outcome);
        Assert.All(deferred.Value.UnresolvedItems, item => Assert.False(item.Acknowledged));
    }

    [Fact]
    public async Task ShouldRejectTypeIEntryGivenLaterAssessmentExists()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var first = await fixture.RunAsync(0);
        await fixture.RunAsync(1);

        // Act
        // Assert
        await PersonalTypeIEntryTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideTypeIEntry(fixture.TenantId, fixture.ProgramId, first.AssessmentId,
                2, "defer", "Stale."), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRecordSupersededAdvisorAnnotationGivenLaterAssessment()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var gap = assessment.Gaps[0];

        // Act
        var annotation = await PersonalReadinessAnnotationTransportTests.HttpAsync(fixture.Provider, fixture.DeciderUserId, new AnnotateReadinessGap(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, gap.GapId, 1, "Consider a quarterly review."));
        await fixture.RunAsync(2);

        // Assert
        Assert.Equal(gap.GapId, annotation.Value.GapId);
        var list = await fixture.QueryAsync<ListReadinessAnnotations,
            Page<ReadinessAnnotationView>>(new ListReadinessAnnotations(fixture.TenantId,
            fixture.ProgramId, assessment.AssessmentId));
        var stored = Assert.Single(list.Items);
        Assert.False(stored.Current);
        Assert.Equal("Consider a quarterly review.", stored.Body);
        await PersonalReadinessAnnotationTransportTests.HttpAsync(fixture.Provider, fixture.DeciderUserId, new AnnotateReadinessGap(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, Uuid.CreateVersion4(), 3, "Unknown gap."), RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldFilterGapsGivenKindRuleAndOwner()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var assessment = await fixture.RunAsync(0);
        var planned = assessment.Gaps[0];
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new PlanReadinessGap(fixture.TenantId, fixture.ProgramId, planned.GapId, 1,
                fixture.OwnerMemberId, new DateOnly(2027, 3, 31), "Close it."))
            .ExpectSuccess();

        // Act
        var byOwner = await fixture.QueryAsync<ListReadinessGaps, Page<ReadinessGapView>>(
            new ListReadinessGaps(fixture.TenantId, fixture.ProgramId, assessment.AssessmentId,
                OwnerMemberId: fixture.OwnerMemberId));
        var byKind = await fixture.QueryAsync<ListReadinessGaps, Page<ReadinessGapView>>(
            new ListReadinessGaps(fixture.TenantId, fixture.ProgramId, assessment.AssessmentId,
                Kind: "no_risks", RuleId: ReadinessRules.RiskResolved));

        // Assert
        Assert.Equal(planned.GapId, Assert.Single(byOwner.Items).GapId);
        Assert.Equal("risks", Assert.Single(byKind.Items).Subject);
    }

    [Fact]
    public async Task ShouldRecordDeclaredCatalogLimitationsGivenApprovedFiveCategoryScope()
    {
        // Arrange
        var catalog = CriteriaCatalog.Platform;
        var fixture = await Fixture.CreateAsync(catalog: catalog,
            selectedEdition: catalog.Edition.EditionId,
            sourceFactory: (tenant, program, asOf) => ReadinessSourceSet.Empty with
            {
                Boundaries = [ReadinessCatalogSupportGapTests.Boundary(tenant, program,
                    ["security", "availability", "processing_integrity", "confidentiality", "privacy"], asOf)],
            });

        // Act
        var assessment = await fixture.RunAsync(0);
        var gaps = await fixture.QueryAsync<ListReadinessGaps, Page<ReadinessGapView>>(
            new ListReadinessGaps(fixture.TenantId, fixture.ProgramId, assessment.AssessmentId,
                Kind: "catalog_support_gap", RuleId: "catalog_support_declared"));

        // Assert
        Assert.Equal("readiness-rules/11", assessment.RuleVersion);
        Assert.Equal(6, gaps.Items.Count);
        Assert.Equal(assessment.Gaps.Count, assessment.GapCount);
        foreach (var gap in gaps.Items)
        {
            var declared = Assert.Single(catalog.Edition.SupportGaps,
                item => item.Category + "|" + item.Code == gap.Subject);
            Assert.Equal(declared.Note, gap.Explanation);
            Assert.Equal(new ReadinessSourceReference("criteria_catalog_edition",
                catalog.Edition.EditionId, catalog.Edition.EditionLabel), Assert.Single(gap.Sources));
        }
    }

    [Fact]
    public async Task ShouldUseExactAsOfEditionMetadataGivenLaterProgramRemap()
    {
        // Arrange
        var baseline = ControlCriterionMappingHandlerTests.TestCatalog();
        var first = baseline.GetEdition(Edition)! with
        {
            SupportGaps = [new CriteriaSupportGap("security", "test_support", "Earlier declared note.")],
        };
        var second = baseline.Editions.Single(item => item.EditionId != Edition) with
        {
            SupportGaps = [new CriteriaSupportGap("security", "test_support", "Later declared note.")],
        };
        var catalog = new CriteriaCatalog([first, second], baseline.Entries);
        ReadinessBoundaryInput? boundary = null;
        var fixture = await Fixture.CreateAsync(catalog: catalog,
            sourceFactory: (tenant, program, _) => ReadinessSourceSet.Empty with
            {
                Boundaries = [boundary ??= ReadinessCatalogSupportGapTests.Boundary(tenant,
                    program, ["security"], DateTimeOffset.UtcNow.AddHours(-4))],
            });
        var remappedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                Command(program.SelectCriteriaEdition(2, second.EditionId, Uuid.CreateVersion4(),
                    "Admin", remappedAt)));

        // Act
        var earlier = await fixture.RunAsync(0, remappedAt.AddMinutes(-1));
        var earlierGap = Assert.Single(earlier.Gaps, gap => gap.Kind == "catalog_support_gap");
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new PlanReadinessGap(fixture.TenantId, fixture.ProgramId, earlierGap.GapId, 1,
                fixture.OwnerMemberId, new DateOnly(2027, 3, 31), "Track the earlier edition."))
            .ExpectSuccess();
        var later = await fixture.RunAsync(2, remappedAt.AddMinutes(1));
        var recordedEarlier = await fixture.GetAsync(earlier.AssessmentId);

        // Assert
        var laterGap = Assert.Single(later.Gaps, gap => gap.Kind == "catalog_support_gap");
        Assert.Equal(first.EditionId, earlier.EditionId);
        Assert.Equal(second.EditionId, later.EditionId);
        Assert.Equal("Earlier declared note.", earlierGap.Explanation);
        Assert.Equal("Later declared note.", laterGap.Explanation);
        Assert.NotEqual(earlierGap.GapId, laterGap.GapId);
        Assert.Null(laterGap.Plan);
        Assert.Equal(earlier.InputFingerprint, recordedEarlier.InputFingerprint);
        var storedEarlierGap = Assert.Single(recordedEarlier.Gaps, gap => gap.Kind == "catalog_support_gap");
        Assert.Equal(earlierGap.GapId, storedEarlierGap.GapId);
        Assert.Equal(earlierGap.Explanation, storedEarlierGap.Explanation);
        Assert.Equal(fixture.OwnerMemberId, storedEarlierGap.Plan?.OwnerMemberId);
    }

    [Fact]
    public async Task ShouldRetainDeclaredGapAndPlanGivenAnnotationAndEquivalentReassessment()
    {
        // Arrange
        var baseline = ControlCriterionMappingHandlerTests.TestCatalog();
        var edition = baseline.GetEdition(Edition)! with
        {
            SupportGaps = [new CriteriaSupportGap("security", "test_support", "Declared limitation remains.")],
        };
        var catalog = new CriteriaCatalog(edition, baseline.ListEntries(Edition, null, null, null));
        ReadinessBoundaryInput? boundary = null;
        var fixture = await Fixture.CreateAsync(catalog: catalog,
            sourceFactory: (tenant, program, _) => ReadinessSourceSet.Empty with
            {
                Boundaries = [boundary ??= ReadinessCatalogSupportGapTests.Boundary(tenant,
                    program, ["security"], DateTimeOffset.UtcNow.AddHours(-4))],
            });
        var asOf = DateTimeOffset.UtcNow.AddHours(-2);
        var first = await fixture.RunAsync(0, asOf);
        var gap = Assert.Single(first.Gaps, item => item.Kind == "catalog_support_gap");
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new PlanReadinessGap(fixture.TenantId, fixture.ProgramId, gap.GapId, 1,
                fixture.OwnerMemberId, new DateOnly(2027, 3, 31), "Track the limitation."))
            .ExpectSuccess();
        await PersonalReadinessAnnotationTransportTests.HttpAsync(fixture.Provider, fixture.DeciderUserId, new AnnotateReadinessGap(fixture.TenantId, fixture.ProgramId,
                first.AssessmentId, gap.GapId, 2, "Acknowledged; declaration remains."));

        // Act
        var repeated = await fixture.RunAsync(3, asOf);
        var retained = Assert.Single(repeated.Gaps, item => item.Kind == "catalog_support_gap");

        // Assert
        Assert.Equal(gap.GapId, retained.GapId);
        Assert.Equal(gap.Explanation, retained.Explanation);
        Assert.NotNull(retained.Plan);
        Assert.Equal(fixture.OwnerMemberId, retained.Plan.OwnerMemberId);
        Assert.Equal(first.InputFingerprint, repeated.InputFingerprint);
        Assert.DoesNotContain(repeated.Findings, finding => finding.RuleId == "catalog_support_declared");
        await PersonalReadinessClosureTransportTests.DecideHttpAsync(fixture.Provider, fixture.DeciderUserId, new DecideReadiness(fixture.TenantId, fixture.ProgramId, repeated.AssessmentId,
                4, "proceed", "Other gaps remain unplanned."), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldKeepCatalogGapGivenMetMappingRulesAndManagementAcknowledgment()
    {
        // Arrange
        var baseline = ControlCriterionMappingHandlerTests.TestCatalog();
        var edition = baseline.GetEdition(Edition)! with
        {
            SupportGaps = [new CriteriaSupportGap("security", "test_support", "Declared limitation remains.")],
        };
        var catalog = new CriteriaCatalog(edition, baseline.ListEntries(Edition, null, null, null));
        ReadinessBoundaryInput? boundary = null;
        var fixture = await Fixture.CreateAsync(catalog: catalog,
            sourceFactory: (tenant, program, _) => ReadinessSourceSet.Empty with
            {
                Boundaries = [boundary ??= ReadinessCatalogSupportGapTests.Boundary(tenant,
                    program, ["security"], DateTimeOffset.UtcNow.AddHours(-4))],
            });
        await fixture.MapAsync("CC6.1", fixture.ReviewedAt);
        await fixture.MapAsync("CC6.2", fixture.ReviewedAt);
        var asOf = DateTimeOffset.UtcNow;
        var assessment = await fixture.RunAsync(0, asOf);
        var gap = Assert.Single(assessment.Gaps, item => item.Kind == "catalog_support_gap");
        await PersonalReadinessAnnotationTransportTests.HttpAsync(fixture.Provider, fixture.DeciderUserId, new AnnotateReadinessGap(fixture.TenantId, fixture.ProgramId,
                assessment.AssessmentId, gap.GapId, 1, "Acknowledged limitation."));

        // Act
        var revision = await fixture.PlanAllAndProceedAsync(await fixture.GetAsync(assessment.AssessmentId));
        var acknowledged = await fixture.GetAsync(assessment.AssessmentId);
        var reassessed = await fixture.RunAsync(revision, asOf);

        // Assert
        Assert.Equal(4, assessment.Findings.Count);
        Assert.All(assessment.Findings, finding => Assert.Equal(ReadinessRules.RuleMet, finding.Outcome));
        Assert.Equal("proceed", acknowledged.Decision?.Outcome);
        foreach (var recorded in new[] { acknowledged, reassessed })
        {
            var retained = Assert.Single(recorded.Gaps, item => item.Kind == "catalog_support_gap");
            Assert.Equal(gap.GapId, retained.GapId);
            Assert.Equal(gap.Explanation, retained.Explanation);
            Assert.NotNull(retained.Plan);
            Assert.DoesNotContain(recorded.Findings, finding => finding.RuleId == "catalog_support_declared");
        }
    }

    static Result Command(CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    internal sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid RunnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid DeciderUserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerMemberId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset ReviewedAt { get; } = DateTimeOffset.UtcNow.AddHours(-1);
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-READY");
        public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);

        public static async Task<Fixture> CreateAsync(DateOnly? effectiveFrom = null,
            bool selectEdition = true, ICriteriaCatalog? catalog = null, Uuid? selectedEdition = null,
            Func<Uuid, Uuid, DateTimeOffset, ReadinessSourceSet>? sourceFactory = null)
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<RunReadinessAssessmentHandler>()
                    .AddRequestHandler<DecideTypeIEntryHandler>()
                    .AddRequestHandler<ListTypeIEntryDecisionsHandler>()
                    .AddRequestHandler<AnnotateReadinessGapHandler>()
                    .AddRequestHandler<ListReadinessAnnotationsHandler>()
                    .AddRequestHandler<GetReadinessAssessmentHandler>()
                    .AddRequestHandler<ListReadinessAssessmentsHandler>()
                    .AddRequestHandler<ListReadinessGapsHandler>()
                    .AddRequestHandler<PlanReadinessGapHandler>()
                    .AddRequestHandler<DecideReadinessHandler>(),
                services => services.AddSingleton<ICriteriaCatalog>(
                        catalog ?? ControlCriterionMappingHandlerTests.TestCatalog())
                    .AddSingleton<IReadinessSourceReader>(new EmptyReadinessSources(sourceFactory))
                    .AddScoped<IReadinessReadModel>(provider =>
                        new EventSourcedReadinessReadModel(
                            provider.GetRequiredService<IAggregateReader>())));
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow.AddDays(-1);
            var admin = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(provider,
                new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                    Command(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null,
                        null), admin, "Admin", now)));
            if (selectEdition)
                await ProgramManagementServices.SeedAsync(provider,
                    new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                        Command(program.SelectCriteriaEdition(1, selectedEdition ?? Edition,
                            admin, "Admin", now)));
            var owner = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                {
                    var created = control.Create(fixture.ProgramId, Uuid.CreateVersion4(),
                        "AC-READY", new ControlDraftContent("Access review", "Review access",
                            "Management reviews access", "Quarterly review.", ["Record"]),
                        admin, "Admin", now);
                    Assert.True(created.IsSuccess);
                    Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control",
                            fixture.ControlId, fixture.ControlVersionId, 1), Uuid.CreateVersion4(),
                        owner, ResponsibilityType.ControlOwner, admin, "Admin", now,
                        now.AddMinutes(-1), null, []));
                    Assert.Null(control.Review(fixture.ProgramId, 1, reviewId, "accept", "Ok",
                        Uuid.CreateVersion4(), "Reviewer", now));
                    return Command(control.Approve(fixture.ProgramId, 1, Uuid.CreateVersion4(),
                        reviewId, effectiveFrom ?? DateOnly.FromDateTime(now.UtcDateTime),
                        "Ready", new HashSet<Uuid> { owner }, Uuid.CreateVersion4(), "Approver",
                        now));
                });
            return fixture;
        }

        public Task MapAsync(string identifier, DateTimeOffset reviewedAt) =>
            ProgramManagementServices.SeedAsync(Provider,
                new ControlCriterionMappingLedger(TenantId, ProgramId), ledger =>
                {
                    var mappingId = ControlCriterionMappingLedger.MappingIdFor(ProgramId,
                        ControlId, Edition, identifier);
                    Assert.Null(ledger.Propose(ControlId, ControlVersionId, Edition, identifier,
                        "criterion", 0, "Access reviews restrict access.", "All systems.",
                        Uuid.CreateVersion4(), "Author", reviewedAt.AddMinutes(-10), out _));
                    return Command(ledger.Review(mappingId, 1, Uuid.CreateVersion4(), "accept",
                        "Matches.", Uuid.CreateVersion4(), "Reviewer", reviewedAt));
                });

        public async Task<long> PlanAllAndProceedAsync(ReadinessAssessmentView assessment)
        {
            var revision = assessment.Revision;
            foreach (var gap in assessment.Gaps)
            {
                await Scenario(RunnerUserId)
                    .When(new PlanReadinessGap(TenantId, ProgramId, gap.GapId, revision,
                        OwnerMemberId, new DateOnly(2027, 3, 31), "Close it."))
                    .ExpectSuccess();
                revision++;
            }
            await PersonalReadinessClosureTransportTests.DecideHttpAsync(Provider, DeciderUserId, new DecideReadiness(TenantId, ProgramId, assessment.AssessmentId,
                    revision, "proceed", "Owned."));
            return revision + 1;
        }

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public async Task<ReadinessAssessmentView> RunAsync(long expectedRevision,
            DateTimeOffset? asOf = null)
        {
            var result = await Scenario(RunnerUserId)
                .When(new RunReadinessAssessment(TenantId, ProgramId, expectedRevision, asOf))
                .ExpectSuccess();
            return await GetAsync(result.Value.AssessmentId);
        }

        public Task<ReadinessAssessmentView> GetAsync(Uuid assessmentId) =>
            QueryAsync<GetReadinessAssessment, ReadinessAssessmentView>(
                new GetReadinessAssessment(TenantId, ProgramId, assessmentId));

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            var result = await Scenario(DeciderUserId).When(request).ExpectSuccess();
            return result.Value;
        }
    }

    sealed class EmptyReadinessSources(
        Func<Uuid, Uuid, DateTimeOffset, ReadinessSourceSet>? sourceFactory = null) : IReadinessSourceReader
    {
        public ValueTask<Result<ReadinessSourceSet>> ReadAsync(Uuid tenantId, Uuid programId,
            DateTimeOffset asOf, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<ReadinessSourceSet>.Success(
                sourceFactory?.Invoke(tenantId, programId, asOf) ?? ReadinessSourceSet.Empty));
    }
}

sealed class EventSourcedReadinessReadModel(IAggregateReader reader) : IReadinessReadModel
{
    public async ValueTask<Result<ReadinessAssessmentView>> GetAssessmentAsync(
        GetReadinessAssessment request, CancellationToken ct)
    {
        var ledger = await LoadAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        return ledger.Read(request.AssessmentId) is { } assessment
            ? Result<ReadinessAssessmentView>.Success(assessment)
            : Result<ReadinessAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The readiness assessment was not found."));
    }

    public async ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
        ListReadinessAssessments request, CancellationToken ct)
    {
        var ledger = await LoadAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        return ControlActivationSource.Paginate(ledger.Summaries(), request.Limit,
            request.Cursor, "readiness assessments");
    }

    public async ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(
        ListReadinessGaps request, CancellationToken ct)
    {
        if (request.PlanState is not (null or "planned" or "unplanned"))
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The plan state filter must be planned or unplanned."));
        var ledger = await LoadAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (ledger.ReadGaps(request.AssessmentId) is not { } gaps)
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The readiness assessment was not found."));
        var items = gaps.Where(gap =>
            (request.PlanState is null ||
             (gap.Plan is not null) == (request.PlanState == "planned")) &&
            (request.OwnerMemberId is not { } owner || gap.Plan?.OwnerMemberId == owner) &&
            (request.Kind is null || gap.Kind == request.Kind) &&
            (request.RuleId is null || gap.RuleId == request.RuleId) &&
            (request.Subject is null || gap.Subject == request.Subject)).ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "readiness gaps");
    }

    public async ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
        ListReadinessAnnotations request, CancellationToken ct)
    {
        var ledger = await LoadAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (ledger.Annotations(request.AssessmentId) is not { } annotations)
            return Result<Page<ReadinessAnnotationView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The readiness assessment was not found."));
        return ControlActivationSource.Paginate(annotations, request.Limit, request.Cursor,
            "readiness annotations");
    }

    public async ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
        ListTypeIEntryDecisions request, CancellationToken ct)
    {
        var ledger = await LoadAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        return ControlActivationSource.Paginate(ledger.TypeIEntryDecisions(), request.Limit,
            request.Cursor, "Type I entry decisions");
    }

    ValueTask<ReadinessLedger> LoadAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct) => reader.HydrateAsync(new ReadinessLedger(tenantId, programId), ct);
}
