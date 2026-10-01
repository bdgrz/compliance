using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
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
        foreach (var family in new[] { "workforce", "technology_inventory", "evidence",
                     "applications_access_review_scope" })
            Assert.Contains(evaluation.Gaps, g => g.Kind == "input_not_assessed" &&
                                                   g.Subject == family);
    }

    [Fact]
    public void ShouldMeetFamilyRulesGivenApprovedBoundaryEffectiveCommitmentTreatedRiskAndSnapshot()
    {
        // Arrange
        var sources = new ReadinessSourceSet([Boundary(AsOf.AddDays(-2), AsOf.AddDays(-2))],
            [Commitment("effective")], [new ReadinessRiskInput(Uuid.CreateVersion4(), "R-1", 3,
                "accepted")], [Snapshot(AsOf.AddHours(-1))]);

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.DoesNotContain(evaluation.Gaps, g => g.RuleId is ReadinessRules.BoundaryApproved
            or ReadinessRules.CommitmentEffective or ReadinessRules.RiskResolved
            or ReadinessRules.ScopeSnapshotFrozen);
    }

    [Fact]
    public void ShouldRecordPerRecordGapsGivenDraftCommitmentUnassessedRiskAndLaterSources()
    {
        // Arrange
        var risk = new ReadinessRiskInput(Uuid.CreateVersion4(), "R-2", 1, "treatment_chosen");
        var sources = new ReadinessSourceSet([Boundary(AsOf.AddDays(1), AsOf.AddDays(-1))],
            [Commitment("reviewed")], [risk], [Snapshot(AsOf.AddHours(1))]);

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
    public void ShouldChangeFingerprintGivenSourceStatusChange()
    {
        // Arrange
        var draft = new ReadinessSourceSet([], [Commitment("reviewed")], [], []);
        var commitment = draft.Commitments[0] with { Status = "effective", Revision = 2 };
        var effective = draft with { Commitments = [commitment] };

        // Act
        var before = Evaluate(draft);
        var after = Evaluate(effective);

        // Assert
        Assert.NotEqual(before.InputFingerprint, after.InputFingerprint);
        Assert.Equal(before.InputFingerprint, Evaluate(draft).InputFingerprint);
    }

    static ReadinessEvaluation Evaluate(ReadinessSourceSet sources) =>
        ReadinessRules.Evaluate(Program, AsOf, null, [], [],
            new Dictionary<Uuid, Bdgrz.Compliance.Features.Controls.ControlVersionView?>(),
            sources);

    static BoundaryView Boundary(DateTimeOffset effectiveFrom, DateTimeOffset approvedAt)
    {
        var boundaryId = Uuid.CreateVersion4();
        var version = new BoundaryVersionView(Tenant, boundaryId, Program, Uuid.CreateVersion4(),
            1, null!, "approved", DateOnly.FromDateTime(effectiveFrom.UtcDateTime),
            Uuid.CreateVersion4(), "Author", approvedAt);
        return new BoundaryView(Tenant, boundaryId, Program, null, version, null, 1);
    }

    static CommitmentDraftView Commitment(string status) => new(Tenant, Program,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", "C-1", 1, status,
        "verified", "resolved", "resolved", "Statement", "Context", "Source",
        Uuid.CreateVersion4(), "Author", AsOf.AddDays(-3));

    static SnapshotView Snapshot(DateTimeOffset frozenAt)
    {
        var id = Uuid.CreateVersion4();
        return new SnapshotView(Tenant, id, id, null, Program, "program_scope", 1, null!, "{}",
            new string('a', 64), null, Uuid.CreateVersion4(), "Author", frozenAt);
    }
}
