using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Version 1 of the manual readiness rules (M0-D23: R1-08 owns rule definitions). Rules
///     evaluate only recorded inputs as of an exact time. A met rule never states that a
///     criterion is satisfied, that controls operate, or that an audit would succeed; source
///     families the rules do not yet assess are recorded as explicit gaps, never as positives.
/// </summary>
public static class ReadinessRules
{
    public const string Version = "readiness-rules/1";
    public const string CriterionMapped = "criterion_has_accepted_mapping";
    public const string MappedControlEffective = "mapped_control_has_effective_version";
    public const string SourceFamilyAssessed = "source_family_assessed";
    public const string RuleMet = "rule_met";
    public const string Gap = "gap";

    /// <summary>Source families whose readiness rules are not yet defined in this version.</summary>
    public static readonly IReadOnlyList<string> UnassessedFamilies =
    [
        "risks", "commitments", "boundaries", "applications_access_review_scope", "workforce",
        "technology_inventory", "population_snapshots", "evidence",
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
        IReadOnlyDictionary<Uuid, ControlVersionView?> effectiveControls)
    {
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
