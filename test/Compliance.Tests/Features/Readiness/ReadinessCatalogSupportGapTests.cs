using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessCatalogSupportGapTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset AsOf = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly CriteriaCatalogEdition Edition = CriteriaCatalog.Platform.Edition;
    static readonly string[] Categories =
    [
        "security", "availability", "processing_integrity", "confidentiality", "privacy",
    ];

    [Fact]
    public void ShouldRecordSixDeclaredLimitationsGivenAllFiveApprovedCategories()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, Categories, AsOf));

        // Act
        var evaluation = Evaluate(sources);
        var gaps = evaluation.Gaps.Where(gap => gap.Kind == "catalog_support_gap").ToArray();

        // Assert
        Assert.Equal(6, gaps.Length);
        foreach (var declared in Edition.SupportGaps)
        {
            var gap = Assert.Single(gaps, gap => gap.Subject == declared.Category + "|" + declared.Code);
            Assert.Equal("catalog_support_declared", gap.RuleId);
            Assert.Equal(declared.Note, gap.Explanation);
            Assert.Equal(ReadinessRules.GapIdFor(Program, gap.RuleId,
                Edition.EditionId + "|" + gap.Subject), gap.GapId);
            Assert.Equal(new ReadinessSourceReference("criteria_catalog_edition",
                Edition.EditionId, Edition.EditionLabel), Assert.Single(gap.Sources));
        }
        Assert.DoesNotContain(evaluation.Findings, finding => finding.RuleId == "catalog_support_declared");
    }

    [Fact]
    public void ShouldRecordOneLimitationGivenDuplicateSecurityCategoriesAcrossBoundaries()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf),
            Boundary(Tenant, Program, ["security"], AsOf));

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Equal("security|points_of_focus_partial", Assert.Single(evaluation.Gaps,
            gap => gap.Kind == "catalog_support_gap").Subject);
    }

    [Fact]
    public void ShouldUnionExplicitScopeGivenDistinctCategoriesAcrossApprovedBoundaries()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program,
                ["security", "availability", "processing_integrity"], AsOf),
            Boundary(Tenant, Program, ["security", "confidentiality", "privacy"], AsOf));

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Equal(6, evaluation.Gaps.Count(gap => gap.Kind == "catalog_support_gap"));
        Assert.Single(evaluation.Gaps, gap => gap.Subject == "security|points_of_focus_partial");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldUseEarlierCategoryScopeGivenLaterApprovalOrFutureEffectiveSuccessor(bool laterApproval)
    {
        // Arrange
        var boundary = Boundary(Tenant, Program, ["security"], AsOf);
        var successor = boundary.ApprovedVersions[0] with
        {
            VersionId = Uuid.CreateVersion4(),
            Revision = 2,
            Content = boundary.ApprovedVersions[0].Content with { TrustServicesCategories = ["security", "privacy"] },
            EffectiveFrom = DateOnly.FromDateTime(AsOf.AddDays(laterApproval ? 0 : 1).UtcDateTime),
        };
        var decision = boundary.Decisions[0] with
        {
            DecisionId = Uuid.CreateVersion4(),
            VersionId = successor.VersionId,
            Revision = 2,
            DecidedAt = AsOf.AddHours(laterApproval ? 1 : -1),
        };
        var sources = Sources(boundary with
        {
            ApprovedVersions = [boundary.ApprovedVersions[0], successor],
            Decisions = [boundary.Decisions[0], decision],
        });

        // Act
        var before = Evaluate(sources);
        var after = Evaluate(sources, asOf: AsOf.AddDays(2));

        // Assert
        Assert.Equal("security|points_of_focus_partial", Assert.Single(before.Gaps,
            gap => gap.Kind == "catalog_support_gap").Subject);
        Assert.Equal(3, after.Gaps.Count(gap => gap.Kind == "catalog_support_gap"));
        Assert.Contains(after.Gaps, gap => gap.Subject == "privacy|privacy_lifecycle_unsupported");
    }

    [Fact]
    public void ShouldKeepMissingCatalogGapGivenUnknownEdition()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, Categories, AsOf));

        // Act
        var evaluation = Evaluate(sources, noEdition: true);

        // Assert
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "input_not_assessed" &&
            gap.Subject == "criteria_catalog");
        Assert.DoesNotContain(evaluation.Gaps, gap => gap.Kind == "catalog_support_gap");
    }

    [Fact]
    public void ShouldAvoidInferringCategoriesGivenNoApprovedBoundary()
    {
        // Arrange
        var draft = Boundary(Tenant, Program, Categories, AsOf) with { Decisions = [] };

        // Act
        var evaluation = Evaluate(Sources(draft));

        // Assert
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "no_approved_boundary");
        Assert.DoesNotContain(evaluation.Gaps, gap => gap.Kind == "catalog_support_gap");
    }

    [Fact]
    public void ShouldKeepTruncationGapGivenPartiallyEnumeratedBoundaryScope()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf)) with
        {
            TruncatedFamilies = ["boundaries"],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "source_truncated" && gap.Subject == "boundaries");
        Assert.Single(evaluation.Gaps, gap => gap.Kind == "catalog_support_gap");
    }

    [Fact]
    public void ShouldKeepFingerprintAndGapIdsGivenReorderedCategoriesBoundariesAndMetadata()
    {
        // Arrange
        var first = Boundary(Tenant, Program, Categories, AsOf);
        var second = Boundary(Tenant, Program, ["security"], AsOf);
        var reversed = first with
        {
            ApprovedVersions = [first.ApprovedVersions[0] with
            {
                Content = first.ApprovedVersions[0].Content with
                {
                    TrustServicesCategories = Categories.Reverse().ToArray(),
                },
            }],
        };

        // Act
        var original = Evaluate(Sources(first, second));
        var reordered = Evaluate(Sources(second, reversed), Edition with
        {
            SupportGaps = Edition.SupportGaps.Reverse().ToArray(),
        });

        // Assert
        Assert.Equal(original.InputFingerprint, reordered.InputFingerprint);
        Assert.Equal(original.Gaps.Select(gap => gap.GapId), reordered.Gaps.Select(gap => gap.GapId));
        Assert.Equal(6, reordered.Gaps.Count(gap => gap.Kind == "catalog_support_gap"));
    }

    [Theory]
    [InlineData("security", false)]
    [InlineData("privacy", true)]
    public void ShouldFingerprintOnlyApplicableMetadataGivenDeclaredNoteChange(string category, bool unchanged)
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf));
        var changed = Edition with
        {
            SupportGaps = Edition.SupportGaps.Select(gap => gap.Category == category
                ? gap with { Note = gap.Note + " Updated declaration." }
                : gap).ToArray(),
        };

        // Act
        var original = Evaluate(sources);
        var revised = Evaluate(sources, changed);

        // Assert
        Assert.Equal(unchanged, original.InputFingerprint == revised.InputFingerprint);
        if (!unchanged)
            Assert.EndsWith(" Updated declaration.", Assert.Single(revised.Gaps,
                gap => gap.Kind == "catalog_support_gap").Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldFingerprintExplicitScopeGivenCategoryWithoutDeclaredLimitation()
    {
        // Arrange
        var boundary = Boundary(Tenant, Program, ["security"], AsOf);
        var expanded = boundary with
        {
            ApprovedVersions = [boundary.ApprovedVersions[0] with
            {
                Content = boundary.ApprovedVersions[0].Content with
                {
                    TrustServicesCategories = ["security", "availability"],
                },
            }],
        };
        var edition = Edition with { SupportGaps = [] };

        // Act
        var original = Evaluate(Sources(boundary), edition);
        var revised = Evaluate(Sources(expanded), edition);

        // Assert
        Assert.NotEqual(original.InputFingerprint, revised.InputFingerprint);
        Assert.DoesNotContain(revised.Gaps, gap => gap.Kind == "catalog_support_gap");
    }

    [Fact]
    public void ShouldFrameExactDeclarationsGivenSeparatorOrNewlineInMetadata()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf));
        var first = Edition with
        {
            SupportGaps = [new CriteriaSupportGap("security", "a|b", "c\ncontinued")],
        };
        var second = Edition with
        {
            SupportGaps = [new CriteriaSupportGap("security", "a", "b|c\ncontinued")],
        };

        // Act
        var original = Evaluate(sources, first);
        var revised = Evaluate(sources, second);

        // Assert
        Assert.NotEqual(original.InputFingerprint, revised.InputFingerprint);
        Assert.Equal(first.SupportGaps[0].Note, Assert.Single(original.Gaps,
            gap => gap.Kind == "catalog_support_gap").Explanation);
    }

    [Fact]
    public void ShouldBindEditionLabelGivenProvenanceChange()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf));
        var relabeled = Edition with { EditionLabel = Edition.EditionLabel + "|revised\nlabel" };

        // Act
        var original = Evaluate(sources);
        var revised = Evaluate(sources, relabeled);

        // Assert
        Assert.NotEqual(original.InputFingerprint, revised.InputFingerprint);
        Assert.Equal(relabeled.EditionLabel, Assert.Single(Assert.Single(revised.Gaps,
            gap => gap.Kind == "catalog_support_gap").Sources).Version);
    }

    [Fact]
    public void ShouldRejectMismatchedMetadataGivenDifferentSelectedEdition()
    {
        // Arrange
        var otherEdition = Uuid.CreateVersion4();

        // Act
        // Assert
        Assert.Throws<ArgumentException>(() => ReadinessRules.Evaluate(Program, AsOf,
            otherEdition, [], [], new Dictionary<Uuid, ControlVersionView?>(),
            ReadinessSourceSet.Empty, Edition));
    }

    [Fact]
    public void ShouldScopeGapIdentityGivenDifferentProgramOrEdition()
    {
        // Arrange
        var sources = Sources(Boundary(Tenant, Program, ["security"], AsOf));
        var nextEdition = Edition with { EditionId = Uuid.CreateVersion4(), EditionLabel = "next" };

        // Act
        var original = Evaluate(sources);
        var otherProgram = Evaluate(sources, programId: Uuid.CreateVersion4());
        var otherEdition = Evaluate(sources, nextEdition);

        // Assert
        var originalGap = Assert.Single(original.Gaps, gap => gap.Kind == "catalog_support_gap");
        var programGap = Assert.Single(otherProgram.Gaps, gap => gap.Kind == "catalog_support_gap");
        var editionGap = Assert.Single(otherEdition.Gaps, gap => gap.Kind == "catalog_support_gap");
        Assert.NotEqual(originalGap.GapId, programGap.GapId);
        Assert.NotEqual(originalGap.GapId, editionGap.GapId);
        Assert.NotEqual(original.InputFingerprint, otherEdition.InputFingerprint);
    }

    internal static ReadinessBoundaryInput Boundary(Uuid tenantId, Uuid programId,
        IReadOnlyList<string> categories, DateTimeOffset asOf)
    {
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        return new ReadinessBoundaryInput(boundaryId,
        [
            new BoundaryVersionView(tenantId, boundaryId, programId, versionId, 1,
                new BoundaryContent("Service boundary", "readiness", categories, []), "approved",
                DateOnly.FromDateTime(asOf.AddDays(-1).UtcDateTime), actor, "Author", asOf.AddDays(-2)),
        ],
        [
            new BoundaryDecisionView(tenantId, boundaryId, Uuid.CreateVersion4(), versionId, 1,
                "approve", actor, "Approver", "Approved", asOf.AddDays(-1), null, null, null),
        ]);
    }

    static ReadinessSourceSet Sources(params ReadinessBoundaryInput[] boundaries) =>
        ReadinessSourceSet.Empty with { Boundaries = boundaries };

    static ReadinessEvaluation Evaluate(ReadinessSourceSet sources,
        CriteriaCatalogEdition? edition = null, DateTimeOffset? asOf = null, bool noEdition = false,
        Uuid? programId = null)
    {
        var selected = noEdition ? null : edition ?? Edition;
        return ReadinessRules.Evaluate(programId ?? Program, asOf ?? AsOf, selected?.EditionId,
            selected is null ? [] : CriteriaCatalog.Platform.ListEntries(Edition.EditionId,
                null, "criterion", null).Select(criterion => criterion with
                {
                    EditionId = selected.EditionId,
                }).ToArray(), [], new Dictionary<Uuid, ControlVersionView?>(), sources, selected);
    }
}
