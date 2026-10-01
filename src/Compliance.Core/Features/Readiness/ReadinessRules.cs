using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Version 2 of the readiness rules (M0-D23: R1-08 owns rule definitions). Rules
///     evaluate only recorded inputs as of an exact time. A met rule never states that a
///     criterion is satisfied, that controls operate, or that an audit would succeed; source
///     families the rules do not yet assess are recorded as explicit gaps, never as positives.
///     Version 2 adds boundary, commitment, risk, and scope-snapshot rules. Risk rule is the
///     conservative provisional R1-08 choice (#492): every program risk must be residual
///     assessed or accepted with an active acceptance; any other status is a gap.
/// </summary>
public static class ReadinessRules
{
    public const string Version = "readiness-rules/2";
    public const string CriterionMapped = "criterion_has_accepted_mapping";
    public const string MappedControlEffective = "mapped_control_has_effective_version";
    public const string SourceFamilyAssessed = "source_family_assessed";
    public const string BoundaryApproved = "program_has_approved_boundary";
    public const string CommitmentEffective = "commitment_effective";
    public const string RiskResolved = "risk_assessed_and_treated";
    public const string ScopeSnapshotFrozen = "program_scope_snapshot_frozen";
    public const string RuleMet = "rule_met";
    public const string Gap = "gap";

    /// <summary>Source families whose readiness rules are not yet defined in this version.</summary>
    public static readonly IReadOnlyList<string> UnassessedFamilies =
    [
        "applications_access_review_scope", "workforce", "technology_inventory", "evidence",
    ];

    public static Uuid GapIdFor(Uuid programId, string ruleId, string subject) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(programId, "readiness-gap"),
            ruleId + "|" + subject);

    /// <summary>The accepted mapping version in force at the as-of time, if any.</summary>
    public static ControlCriterionMappingVersionView? ActiveAt(ControlCriterionMappingView mapping,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Versions.LastOrDefault(version =>
            version.Status is "accepted" or "superseded" or "retired" &&
            version.ReviewedAt is { } reviewed && reviewed <= asOf &&
            !(version.RetiredAt is { } retired && retired <= asOf));
    }

    public static ReadinessEvaluation Evaluate(Uuid programId, DateTimeOffset asOf,
        Uuid? editionId, IReadOnlyList<Criterion> criteria,
        IReadOnlyList<ControlCriterionMappingView> mappings,
        IReadOnlyDictionary<Uuid, ControlVersionView?> effectiveControls,
        ReadinessSourceSet? sources = null)
    {
        sources ??= ReadinessSourceSet.Empty;
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(effectiveControls);
        var findings = new List<ReadinessFindingView>();
        var gaps = new List<ReadinessGapView>();
        var fingerprint = new StringBuilder()
            .Append(Version).Append('\n')
            .Append(asOf.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)).Append('\n')
            .Append(editionId?.ToString() ?? "-").Append('\n');
        var active = mappings
            .Select(mapping => (Mapping: mapping, Version: ActiveAt(mapping, asOf)))
            .Where(static pair => pair.Version is not null)
            .ToLookup(static pair => pair.Mapping.CriterionIdentifier, StringComparer.Ordinal);
        foreach (var criterion in criteria)
        {
            fingerprint.Append("criterion|").Append(criterion.Identifier).Append('\n');
            var mapped = active[criterion.Identifier]
                .OrderBy(static pair => pair.Mapping.MappingId.ToString(), StringComparer.Ordinal)
                .ToArray();
            var mappingSources = mapped.Select(static pair => new ReadinessSourceReference(
                "control_criterion_mapping", pair.Mapping.MappingId,
                pair.Version!.VersionNumber.ToString(CultureInfo.InvariantCulture))).ToArray();
            foreach (var (mapping, version) in mapped)
                fingerprint.Append("mapping|").Append(mapping.MappingId).Append('|')
                    .Append(version!.VersionNumber).Append('|').Append(version.ControlVersionId)
                    .Append('\n');
            if (mapped.Length == 0)
            {
                findings.Add(AddGap(programId, criterion, CriterionMapped, "unmapped_criterion",
                    "No accepted control mapping was in force at the as-of time; draft, pending, rejected, and retired mappings do not count.",
                    [], gaps));
                continue;
            }
            findings.Add(new ReadinessFindingView(criterion.Identifier, criterion.Category,
                criterion.Summary, CriterionMapped, RuleMet,
                $"{mapped.Length} accepted control mapping(s) were in force at the as-of time.",
                mappingSources, null));
            var controls = mapped.Select(static pair => pair.Mapping.ControlId).Distinct()
                .Select(controlId => effectiveControls.GetValueOrDefault(controlId))
                .OfType<ControlVersionView>()
                .OrderBy(static control => control.ControlId.ToString(), StringComparer.Ordinal)
                .ToArray();
            foreach (var control in controls)
                fingerprint.Append("control|").Append(control.ControlId).Append('|')
                    .Append(control.VersionId).Append('\n');
            findings.Add(controls.Length == 0
                ? AddGap(programId, criterion, MappedControlEffective, "no_effective_control",
                    "No mapped control had an approved version effective on the as-of date.",
                    mappingSources, gaps)
                : new ReadinessFindingView(criterion.Identifier, criterion.Category,
                    criterion.Summary, MappedControlEffective, RuleMet,
                    $"{controls.Length} mapped control(s) had an approved version effective on the as-of date. This does not show that the controls operate or that evidence exists.",
                    controls.Select(static control => new ReadinessSourceReference(
                        "control_version", control.VersionId,
                        control.Revision.ToString(CultureInfo.InvariantCulture))).ToArray(),
                    null));
        }

        var inputs = new List<ReadinessInputView>();
        if (editionId is null)
        {
            inputs.Add(new ReadinessInputView("criteria_catalog", "not_assessed", 0,
                "The program has not selected a criteria edition, so no criterion was assessed."));
            gaps.Add(new ReadinessGapView(GapIdFor(programId, SourceFamilyAssessed,
                    "criteria_catalog"), "input_not_assessed", "criteria_catalog",
                SourceFamilyAssessed, "Select a criteria edition so criteria can be assessed.",
                []));
        }
        else
        {
            inputs.Add(new ReadinessInputView("criteria_catalog", "assessed", criteria.Count,
                "Criteria of the program's selected edition; points of focus are not assessed separately."));
        }
        inputs.Add(new ReadinessInputView("control_criterion_mappings", "assessed",
            active.Sum(static group => group.Count()),
            "Accepted mapping versions in force at the as-of time."));
        inputs.Add(new ReadinessInputView("controls", "assessed",
            effectiveControls.Values.Count(static control => control is not null),
            "Approved control versions effective on the as-of date for mapped controls."));
        EvaluateSources(programId, asOf, sources, inputs, gaps, fingerprint);
        foreach (var family in UnassessedFamilies)
        {
            inputs.Add(new ReadinessInputView(family, "not_assessed", 0,
                $"Readiness rules {Version} do not assess {family}; it remains an acknowledged gap."));
            gaps.Add(new ReadinessGapView(GapIdFor(programId, SourceFamilyAssessed, family),
                "input_not_assessed", family, SourceFamilyAssessed,
                $"The {family} source family was not assessed by {Version}; review it manually.",
                []));
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString()));
        return new ReadinessEvaluation(inputs, findings, gaps,
            Convert.ToHexStringLower(hash));
    }

    static void EvaluateSources(Uuid programId, DateTimeOffset asOf, ReadinessSourceSet sources,
        List<ReadinessInputView> inputs, List<ReadinessGapView> gaps, StringBuilder fingerprint)
    {
        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);

        var approved = sources.Boundaries
            .Select(boundary => BoundaryVersionAt(boundary, asOf, asOfDate))
            .OfType<BoundaryVersionView>()
            .OrderBy(static version => version.BoundaryId.ToString(), StringComparer.Ordinal)
            .ToArray();
        foreach (var version in approved)
            fingerprint.Append("boundary|").Append(version.BoundaryId).Append('|')
                .Append(version.VersionId).Append('\n');
        inputs.Add(new ReadinessInputView("boundaries", "assessed", approved.Length,
            "Boundary versions approved by the as-of time and effective on the as-of date; drafts and pending reviews do not count."));
        if (approved.Length == 0)
            gaps.Add(new ReadinessGapView(GapIdFor(programId, BoundaryApproved, "boundaries"),
                "no_approved_boundary", "boundaries", BoundaryApproved,
                "No approved system boundary version was effective on the as-of date.", []));

        var commitments = sources.Commitments
            .Where(commitment => commitment.CreatedAt <= asOf)
            .Select(commitment => (Commitment: commitment,
                Version: CommitmentVersionAt(commitment, asOf, asOfDate)))
            .OrderBy(static pair => pair.Commitment.Identifier, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Commitment.DraftId.ToString(), StringComparer.Ordinal)
            .ToArray();
        foreach (var (commitment, version) in commitments)
            fingerprint.Append("commitment|").Append(commitment.DraftId).Append('|')
                .Append(version?.Version ?? 0).Append('\n');
        inputs.Add(new ReadinessInputView("commitments", "assessed", commitments.Length,
            "Commitments recorded by the as-of time; only a version approved by then and effective on the as-of date meets the rule."));
        if (commitments.Length == 0)
            gaps.Add(new ReadinessGapView(GapIdFor(programId, CommitmentEffective, "commitments"),
                "no_commitments", "commitments", CommitmentEffective,
                "The program records no service commitments or system requirements.", []));
        foreach (var (commitment, _) in commitments.Where(static pair => pair.Version is null))
            gaps.Add(new ReadinessGapView(GapIdFor(programId, CommitmentEffective,
                    commitment.DraftId.ToString()), "commitment_not_effective",
                commitment.Identifier, CommitmentEffective,
                $"Commitment {commitment.Identifier} had no approved version in force at the as-of time.",
                [new ReadinessSourceReference("commitment", commitment.DraftId, "0")]));

        var risks = sources.Risks
            .Where(risk => risk.CreatedAt <= asOf)
            .OrderBy(static risk => risk.Identifier, StringComparer.Ordinal)
            .ThenBy(static risk => risk.RiskId.ToString(), StringComparer.Ordinal)
            .ToArray();
        foreach (var risk in risks)
            fingerprint.Append("risk|").Append(risk.RiskId).Append('|').Append(risk.Revision)
                .Append('|').Append(risk.EvaluationStatus).Append('\n');
        inputs.Add(new ReadinessInputView("risks", "assessed", risks.Length,
            "Risks recorded by the as-of time; only residual-assessed risks or risks with an active acceptance at that time meet the rule."));
        if (risks.Length == 0)
            gaps.Add(new ReadinessGapView(GapIdFor(programId, RiskResolved, "risks"), "no_risks",
                "risks", RiskResolved, "The program records no assessed risks.", []));
        foreach (var risk in risks.Where(static r =>
                     r.EvaluationStatus is not ("accepted" or "residual_assessed")))
            gaps.Add(new ReadinessGapView(GapIdFor(programId, RiskResolved,
                    risk.RiskId.ToString()), "risk_unresolved", risk.Identifier, RiskResolved,
                $"Risk {risk.Identifier} was {risk.EvaluationStatus} at the as-of time; it needs a residual assessment or an active acceptance.",
                [new ReadinessSourceReference("risk", risk.RiskId,
                    risk.Revision.ToString(CultureInfo.InvariantCulture))]));

        var frozen = sources.Snapshots
            .Where(snapshot => snapshot.Kind == "program_scope" && snapshot.FrozenAt <= asOf)
            .OrderBy(static snapshot => snapshot.SnapshotId.ToString(), StringComparer.Ordinal)
            .ToArray();
        foreach (var snapshot in frozen)
            fingerprint.Append("snapshot|").Append(snapshot.SnapshotId).Append('|')
                .Append(snapshot.ContentSha256).Append('\n');
        inputs.Add(new ReadinessInputView("population_snapshots", "assessed", frozen.Length,
            "Program scope snapshots frozen at or before the as-of time."));
        if (frozen.Length == 0)
            gaps.Add(new ReadinessGapView(GapIdFor(programId, ScopeSnapshotFrozen,
                    "population_snapshots"), "no_scope_snapshot", "population_snapshots",
                ScopeSnapshotFrozen, "No program scope snapshot was frozen by the as-of time.",
                []));

        foreach (var family in sources.TruncatedFamilies.Order(StringComparer.Ordinal))
        {
            fingerprint.Append("truncated|").Append(family).Append('\n');
            gaps.Add(new ReadinessGapView(GapIdFor(programId, SourceFamilyAssessed,
                    "truncated|" + family), "source_truncated", family, SourceFamilyAssessed,
                $"The {family} source family exceeded the readiness read limit; records beyond it were not assessed.",
                []));
        }
    }

    /// <summary>The boundary version approved by the as-of time with the latest effective date on or before it.</summary>
    static BoundaryVersionView? BoundaryVersionAt(ReadinessBoundaryInput boundary,
        DateTimeOffset asOf, DateOnly asOfDate)
    {
        var approvedBy = boundary.Decisions
            .Where(decision => decision.Outcome == "approve" && decision.DecidedAt <= asOf)
            .Select(static decision => decision.VersionId)
            .ToHashSet();
        return boundary.ApprovedVersions
            .Where(version => approvedBy.Contains(version.VersionId) &&
                              version.EffectiveFrom is { } from && from <= asOfDate)
            .MaxBy(static version => version.EffectiveFrom);
    }

    /// <summary>The commitment version approved by the as-of time and effective on the as-of date.</summary>
    static CommitmentVersionView? CommitmentVersionAt(ReadinessCommitmentInput commitment,
        DateTimeOffset asOf, DateOnly asOfDate) =>
        commitment.Versions
            .Where(version => (version.Approval ?? version.Decision).DecidedAt <= asOf &&
                              version.EffectiveFrom <= asOfDate)
            .MaxBy(static version => version.Version);

    static ReadinessFindingView AddGap(Uuid programId, Criterion criterion, string ruleId,
        string kind, string explanation, IReadOnlyList<ReadinessSourceReference> sources,
        List<ReadinessGapView> gaps)
    {
        var gapId = GapIdFor(programId, ruleId, criterion.Identifier);
        gaps.Add(new ReadinessGapView(gapId, kind, criterion.Identifier, ruleId, explanation,
            sources));
        return new ReadinessFindingView(criterion.Identifier, criterion.Category,
            criterion.Summary, ruleId, Gap, explanation, sources, gapId);
    }
}
