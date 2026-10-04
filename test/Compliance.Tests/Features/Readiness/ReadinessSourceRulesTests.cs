using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessSourceRulesTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset AsOf = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordFamilyGapsGivenProgramWithoutBoundaryCommitmentRiskOrSnapshot()
    {
        // Arrange
        var sources = ReadinessSourceSet.Empty;

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_commitments");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_risks");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_scope_snapshot");
        foreach (var family in new[] { "boundaries", "commitments", "risks", "population_snapshots" })
            Assert.Equal("assessed", Assert.Single(evaluation.Inputs, i => i.Family == family)
                .Status);
        Assert.Equal("assessed", Assert.Single(evaluation.Inputs,
            i => i.Family == "applications_access_review_scope").Status);
        Assert.Equal("assessed", Assert.Single(evaluation.Inputs,
            i => i.Family == "technology_inventory").Status);
        foreach (var family in new[] { "workforce", "evidence" })
            Assert.Contains(evaluation.Gaps, g => g.Kind == "input_not_assessed" &&
                                                   g.Subject == family);
    }

    [Fact]
    public void ShouldAssessProviderAndEmitStableReviewGapGivenMaterialProviderWithoutReview()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var provider = new ProviderView(Tenant, providerId, 1,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material",
                Subservice: true, BoundaryTreatment: "carve_out"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var family = Assert.Single(evaluation.Inputs, input => input.Family == "providers");
        Assert.Equal("assessed", family.Status);
        Assert.Equal(1, family.RecordCount);
        var gap = Assert.Single(evaluation.Gaps, item =>
            item.Kind == "provider_review_incomplete" && item.Subject == providerId.ToString());
        Assert.Equal(ReadinessRules.GapIdFor(Program, ReadinessRules.ProviderReviewCurrent,
            providerId.ToString()), gap.GapId);
        Assert.DoesNotContain(evaluation.Gaps, item => item.Kind == "input_not_assessed" &&
            item.Subject == "providers");
    }

    [Fact]
    public void ShouldLinkUnresolvedProviderCoverageGapToReadinessGapGivenAssessmentAsOfTime()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var sourceGapId = Uuid.CreateVersion4();
        var resolvedGapId = Uuid.CreateVersion4();
        var provider = new ProviderView(Tenant, providerId, 2,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [],
            [
                CoverageGap(providerId, sourceGapId, 2, "closed", AsOf.AddDays(-1), AsOf.AddDays(1)),
                CoverageGap(providerId, resolvedGapId, 2, "closed", AsOf.AddDays(-2), AsOf.AddHours(-1)),
            ])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var linkedGap = Assert.Single(evaluation.Gaps, gap => gap.Kind == "provider_coverage_gap_unresolved");
        Assert.Equal(ReadinessRules.GapIdFor(Program, ReadinessRules.ProviderCoverageGapUnresolved,
            sourceGapId.ToString()), linkedGap.GapId);
        Assert.Contains(linkedGap.Sources, source => source.Kind == "provider_coverage_gap" &&
            source.Id == sourceGapId && source.Version == "1");
        Assert.DoesNotContain(evaluation.Gaps, gap => gap.Subject == resolvedGapId.ToString());
    }

    [Fact]
    public void ShouldKeepProviderGapOpenAndRedactRestrictedDetailsGivenAcceptedExposure()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var sourceGapId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var acceptanceId = Uuid.CreateVersion4();
        var acceptedUntil = AsOf.AddDays(30);
        const string restrictedDescription = "Private audit detail about the provider control gap.";
        var provider = new ProviderView(Tenant, providerId, 1,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var acceptance = new ProviderCoverageGapRiskAcceptanceView(Program, riskId,
            acceptanceId, acceptedUntil, ActorReference.ForMember(Uuid.CreateVersion4(), "Jordan Approver"),
            AsOf.AddDays(-1))
        { Revision = 2 };
        var sourceGap = CoverageGap(providerId, sourceGapId, 2, "open", AsOf.AddDays(-2), null);
        var gap = sourceGap with
        {
            Content = sourceGap.Content with
            {
                Description = restrictedDescription,
            },
            RiskAcceptances = [acceptance],
        };
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [], [gap])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var linkedGap = Assert.Single(evaluation.Gaps, item => item.Kind == "provider_coverage_gap_unresolved");
        Assert.Contains(linkedGap.Sources, source => source.Kind == "provider_coverage_gap" &&
            source.Id == sourceGapId && source.Version == "2");
        Assert.DoesNotContain(linkedGap.Sources, source => source.Kind is "risk" or "risk_acceptance");
        Assert.DoesNotContain(acceptanceId.ToString(), linkedGap.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain(riskId.ToString(), linkedGap.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain(restrictedDescription, linkedGap.Explanation, StringComparison.Ordinal);
        Assert.Contains("remains uncovered", linkedGap.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldMeetFamilyRulesGivenApprovedBoundaryEffectiveCommitmentTreatedRiskAndSnapshot()
    {
        // Arrange
        var sources = new ReadinessSourceSet(
            [Boundary((AsOf.AddDays(-2), AsOf.AddDays(-2)))],
            [Commitment(AsOf.AddDays(-10), AsOf.AddDays(-3))],
            [Risk(AsOf.AddDays(-10), "accepted")], [Snapshot(AsOf.AddHours(-1))]);

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.DoesNotContain(evaluation.Gaps, g => g.RuleId is ReadinessRules.BoundaryApproved
            or ReadinessRules.CommitmentEffective or ReadinessRules.RiskResolved
            or ReadinessRules.ScopeSnapshotFrozen);
    }

    [Fact]
    public void ShouldRecordPerRecordGapsGivenUnapprovedCommitmentUnassessedRiskAndLaterSources()
    {
        // Arrange
        var risk = Risk(AsOf.AddDays(-5), "treatment_chosen");
        var sources = new ReadinessSourceSet([Boundary((AsOf.AddDays(1), AsOf.AddDays(-1)))],
            [Commitment(AsOf.AddDays(-5), null)], [risk], [Snapshot(AsOf.AddHours(1))]);

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "commitment_not_effective" &&
                                               g.Subject == "C-1");
        var riskGap = Assert.Single(evaluation.Gaps, g => g.Kind == "risk_unresolved");
        Assert.Equal(risk.RiskId, Assert.Single(riskGap.Sources).Id);
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_scope_snapshot");
    }

    [Fact]
    public void ShouldIgnoreLaterApprovalAndLaterRiskGivenAssessmentAsOfLastMonth()
    {
        // Arrange
        var lastMonth = AsOf.AddMonths(-1);
        var commitment = Commitment(AsOf.AddMonths(-2), AsOf.AddDays(-1));
        var laterRisk = Risk(AsOf.AddDays(-2), "accepted");
        var sources = new ReadinessSourceSet([], [commitment], [laterRisk], []);

        // Act
        var evaluation = Evaluate(sources, lastMonth);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "commitment_not_effective" &&
                                               g.Sources[0].Version == "0");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_risks");
        Assert.Equal(0, Assert.Single(evaluation.Inputs, i => i.Family == "risks").RecordCount);
    }

    [Fact]
    public void ShouldUseEarlierApprovedBoundaryGivenLatestApprovalAfterAsOf()
    {
        // Arrange
        var boundary = Boundary((AsOf.AddDays(-30), AsOf.AddDays(-30)),
            (AsOf.AddDays(1), AsOf.AddDays(1)));

        // Act
        var evaluation = Evaluate(new ReadinessSourceSet([boundary], [], [], []));

        // Assert
        Assert.DoesNotContain(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Equal(1, Assert.Single(evaluation.Inputs, i => i.Family == "boundaries")
            .RecordCount);
    }

    [Fact]
    public void ShouldRecordTruncationGapGivenFamilyExceedsScanLimit()
    {
        // Arrange
        var sources = ReadinessSourceSet.Empty with { TruncatedFamilies = ["risks"] };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "source_truncated" &&
                                               g.Subject == "risks");
    }

    [Fact]
    public void ShouldChangeFingerprintGivenApprovalInForce()
    {
        // Arrange
        var unapproved = new ReadinessSourceSet([], [Commitment(AsOf.AddDays(-9), null)], [],
            []);
        var approved = new ReadinessSourceSet([],
            [Commitment(AsOf.AddDays(-9), AsOf.AddDays(-1))], [], []);

        // Act
        var before = Evaluate(unapproved);
        var after = Evaluate(approved);

        // Assert
        Assert.NotEqual(before.InputFingerprint, after.InputFingerprint);
    }

    static ReadinessEvaluation Evaluate(ReadinessSourceSet sources, DateTimeOffset? asOf = null) =>
        ReadinessRules.Evaluate(Program, asOf ?? AsOf, null, [], [],
            new Dictionary<Uuid, ControlVersionView?>(), sources);

    static ReadinessBoundaryInput Boundary(
        params (DateTimeOffset EffectiveFrom, DateTimeOffset ApprovedAt)[] approvals)
    {
        var boundaryId = Uuid.CreateVersion4();
        var versions = new List<BoundaryVersionView>();
        var decisions = new List<BoundaryDecisionView>();
        foreach (var (effectiveFrom, approvedAt) in approvals)
        {
            var versionId = Uuid.CreateVersion4();
            versions.Add(new BoundaryVersionView(Tenant, boundaryId, Program, versionId,
                versions.Count + 1, null!, "approved",
                DateOnly.FromDateTime(effectiveFrom.UtcDateTime), Uuid.CreateVersion4(),
                "Author", approvedAt.AddDays(-1)));
            decisions.Add(new BoundaryDecisionView(Tenant, boundaryId, Uuid.CreateVersion4(),
                versionId, versions.Count, "approve", Uuid.CreateVersion4(), "Approver", "Ok",
                approvedAt, null, null, null));
        }
        return new ReadinessBoundaryInput(boundaryId, versions, decisions);
    }

    static ReadinessCommitmentInput Commitment(DateTimeOffset createdAt,
        DateTimeOffset? approvedAt)
    {
        var draftId = Uuid.CreateVersion4();
        IReadOnlyList<CommitmentVersionView> versions = approvedAt is { } at
            ?
            [
                new CommitmentVersionView(Tenant, Program, draftId, Uuid.CreateVersion4(),
                    "service_commitment", "C-1", 1, 2, "Statement", "Context", "Source",
                    "Owner", "applicable", "Interpretation", null, "organization", true,
                    DateOnly.FromDateTime(at.UtcDateTime),
                    new CommitmentDecisionView(Tenant, Program, draftId, Uuid.CreateVersion4(),
                        2, "approve", null, null, null, null, "Ok", 1,
                        DateOnly.FromDateTime(at.UtcDateTime), null, Uuid.CreateVersion4(),
                        "Approver", at, null)),
            ]
            : [];
        return new ReadinessCommitmentInput(draftId, "C-1", createdAt, versions);
    }

    static ReadinessRiskInput Risk(DateTimeOffset createdAt, string status) =>
        new(Uuid.CreateVersion4(), "R-1", createdAt, 1, status);

    static ProviderCoverageGapView CoverageGap(Uuid providerId, Uuid gapId, long revision,
        string status, DateTimeOffset recordedAt, DateTimeOffset? closedAt) => new(Tenant,
        gapId, providerId, 1, revision,
        new ProviderCoverageGapContent(Uuid.CreateVersion4(), "Payroll processing", "Availability",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report",
            Uuid.CreateVersion4(), 1, "Coverage does not meet the period."), status,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), recordedAt,
        closedAt is { } at
            ? new ProviderCoverageGapClosureView(new ProviderCoverageGapClosureContent(
                "coverage_restored", "assurance_report", Uuid.CreateVersion4(), 2,
                "Later report restores coverage."), ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), at)
            {
                Revision = revision,
            }
            : null, []);

    static SnapshotView Snapshot(DateTimeOffset frozenAt)
    {
        var id = Uuid.CreateVersion4();
        return new SnapshotView(Tenant, id, id, null, Program, "program_scope", 1, null!, "{}",
            new string('a', 64), null, Uuid.CreateVersion4(), "Author", frozenAt);
    }
}
