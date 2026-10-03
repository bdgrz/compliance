using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Version 7 of the readiness rules (M0-D23: R1-08 owns rule definitions). Rules
///     evaluate only recorded inputs as of an exact time. A met rule never states that a
///     criterion is satisfied, that controls operate, or that an audit would succeed; source
///     families the rules do not yet assess are recorded as explicit gaps, never as positives.
///     Version 2 added boundary, commitment, risk, and scope-snapshot rules. Version 3
///     acknowledged providers as not assessed. Version 4 selects the criteria edition that was
///     in force at the requested as-of time. Version 5 adds provider review and CSOC rules.
///     Version 6 adds effective as-of access-review scope rules. Version 7 selects
///     technology inventory revisions referenced by the as-of boundary. Risk rule is the
///     conservative provisional R1-08 choice (#492): every program risk must be residual
///     assessed or accepted with an active acceptance; any other status is a gap.
/// </summary>
public static class ReadinessRules
{
    public const string Version = "readiness-rules/7";
    public const string CriterionMapped = "criterion_has_accepted_mapping";
    public const string MappedControlEffective = "mapped_control_has_effective_version";
    public const string SourceFamilyAssessed = "source_family_assessed";
    public const string BoundaryApproved = "program_has_approved_boundary";
    public const string CommitmentEffective = "commitment_effective";
    public const string RiskResolved = "risk_assessed_and_treated";
    public const string ScopeSnapshotFrozen = "program_scope_snapshot_frozen";
    public const string ProviderMaterialityResolved = "provider_materiality_resolved";
    public const string ProviderReviewCurrent = "material_provider_has_current_review";
    public const string ProviderCsocLinked = "carved_out_subservice_has_csoc";
    public const string AccessReviewScopeResolved = "application_access_review_scope_decided";
    public const string AccessReviewScopeCurrent = "application_access_review_scope_current";
    public const string TechnologyInventoryRevisionAt = "technology_inventory_revision_at_as_of";
    public const string TechnologyInventoryActive = "technology_inventory_record_active_at_as_of";
    public const string TechnologyInventoryPresent = "program_has_technology_inventory";
    public const string RuleMet = "rule_met";
    public const string Gap = "gap";

    /// <summary>Source families whose readiness rules are not yet defined in this version.</summary>
    public static readonly IReadOnlyList<string> UnassessedFamilies =
    [
        "workforce", "evidence",
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

        EvaluateProviders(programId, asOf, sources, inputs, gaps, fingerprint);
        EvaluateAccessReviewScopes(programId, asOf, sources, inputs, gaps, fingerprint);
        EvaluateTechnologyInventory(programId, asOf, sources, inputs, gaps, fingerprint);

        foreach (var family in sources.TruncatedFamilies.Order(StringComparer.Ordinal))
        {
            fingerprint.Append("truncated|").Append(family).Append('\n');
            gaps.Add(new ReadinessGapView(GapIdFor(programId, SourceFamilyAssessed,
                    "truncated|" + family), "source_truncated", family, SourceFamilyAssessed,
                $"The {family} source family exceeded the readiness read limit; records beyond it were not assessed.",
                []));
        }
    }

    static void EvaluateProviders(Uuid programId, DateTimeOffset asOf,
        ReadinessSourceSet sources, List<ReadinessInputView> inputs,
        List<ReadinessGapView> gaps, StringBuilder fingerprint)
    {
        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);
        var providers = sources.Providers
            .OrderBy(static input => input.Provider.ProviderId.ToString(), StringComparer.Ordinal)
            .ToArray();
        inputs.Add(new ReadinessInputView("providers", "assessed", providers.Length,
            "Provider declarations, reviews and linked CSOCs retained by the as-of time."));

        var csocs = sources.Commitments
            .Where(commitment => commitment.CreatedAt <= asOf)
            .Select(commitment => (Commitment: commitment,
                Version: CommitmentVersionAt(commitment, asOf, asOfDate)))
            .Where(static pair => IsApplicableProviderCsoc(pair.Version))
            .ToArray();

        foreach (var input in providers)
        {
            var provider = input.Provider;
            var providerReference = new ReadinessSourceReference("provider", provider.ProviderId,
                provider.Revision.ToString(CultureInfo.InvariantCulture));
            var unresolvedMateriality = provider.Unresolved.Contains("materiality",
                StringComparer.Ordinal);
            fingerprint.Append("provider|").Append(provider.ProviderId).Append('|')
                .Append(provider.Revision).Append('|').Append(provider.Content.Materiality)
                .Append('|').Append(provider.Content.Subservice).Append('|')
                .Append(provider.Content.BoundaryTreatment).Append('\n');
            foreach (var report in input.Reports.OrderBy(static item => item.ReportId.ToString(),
                         StringComparer.Ordinal))
                fingerprint.Append("provider-report|").Append(report.ReportId).Append('|')
                    .Append(report.Revision).Append('\n');
            foreach (var review in input.Reviews.OrderBy(static item => item.ReviewId.ToString(),
                         StringComparer.Ordinal))
                fingerprint.Append("provider-review|").Append(review.ReviewId).Append('|')
                    .Append(review.ProviderRevision).Append('|')
                    .Append(review.AssuranceReportRevision).Append('|')
                    .Append(review.Content.ReviewedAt.ToString("O", CultureInfo.InvariantCulture))
                    .Append('|').Append(review.Content.NextReviewDue.ToString("O", CultureInfo.InvariantCulture))
                    .Append('|').Append(review.Content.Conclusion).Append('\n');

            if (unresolvedMateriality)
                gaps.Add(new ReadinessGapView(GapIdFor(programId,
                        ProviderMaterialityResolved, provider.ProviderId.ToString()),
                    "provider_materiality_unresolved", provider.ProviderId.ToString(),
                    ProviderMaterialityResolved,
                    $"Provider {provider.Content.Name} has unresolved materiality; classify it with a bounded rationale and basis.",
                    [providerReference]));

            var latestReview = input.Reviews
                .Where(review => review.RecordedAt <= asOf &&
                                 review.Content.ReviewedAt <= asOfDate)
                .OrderByDescending(static review => review.Content.ReviewedAt)
                .ThenByDescending(static review => review.RecordedAt)
                .ThenByDescending(static review => review.ReviewId)
                .FirstOrDefault();
            IReadOnlyCollection<AssuranceReportView> selectedReport = [];
            if (latestReview?.Content.AssuranceReportId is { } linkedReportId &&
                latestReview.AssuranceReportRevision is { } linkedReportRevision)
                selectedReport = input.Reports.Where(report =>
                    report.ReportId == linkedReportId &&
                    report.Revision == linkedReportRevision).ToArray();
            var coverage = ProviderAssuranceCoverage.Evaluate(provider, selectedReport,
                input.Reviews, asOfDate);
            var reviewMatchesProvider = latestReview?.ProviderRevision == provider.Revision;
            var externalEvidence = latestReview is { Content.AssuranceReportId: null, Content.Evidence: not null };
            var reviewInForce = latestReview is not null &&
                                latestReview.Content.Conclusion != "not_acceptable" &&
                                latestReview.Content.NextReviewDue >= asOfDate &&
                                reviewMatchesProvider &&
                                (externalEvidence || coverage.Status == "current");
            var reviewSources = new List<ReadinessSourceReference> { providerReference };
            if (latestReview is not null)
            {
                reviewSources.Add(new ReadinessSourceReference("provider_review",
                    latestReview.ReviewId,
                    latestReview.ProviderRevision.ToString(CultureInfo.InvariantCulture)));
                if (latestReview.Content.AssuranceReportId is { } sourceReportId &&
                    latestReview.AssuranceReportRevision is { } sourceReportRevision)
                    reviewSources.Add(new ReadinessSourceReference("assurance_report",
                        sourceReportId, sourceReportRevision.ToString(CultureInfo.InvariantCulture)));
                else if (latestReview.Content.Evidence?.ArtifactId is { } artifactId)
                    reviewSources.Add(new ReadinessSourceReference("evidence_artifact", artifactId,
                        null));
            }
            if (provider.Content.Materiality != "not_material" && !reviewInForce)
            {
                var detail = !reviewMatchesProvider && latestReview is not null
                    ? "The latest review refers to a different provider revision."
                    : $"The as-of assurance status is {coverage.Status}; an in-force review and its recorded evidence are required.";
                var explanation = $"Material provider {provider.Content.Name} needs a current review for its as-of provider revision. {detail}";
                gaps.Add(new ReadinessGapView(GapIdFor(programId, ProviderReviewCurrent,
                        provider.ProviderId.ToString()), "provider_review_incomplete",
                    provider.ProviderId.ToString(), ProviderReviewCurrent, explanation,
                    reviewSources));
            }

            var providerCsocs = csocs.Where(pair => pair.Version!.ProviderId == provider.ProviderId)
                .OrderBy(static pair => pair.Commitment.DraftId.ToString(), StringComparer.Ordinal)
                .ToArray();
            foreach (var csoc in providerCsocs)
                fingerprint.Append("provider-csoc|").Append(provider.ProviderId).Append('|')
                    .Append(csoc.Commitment.DraftId).Append('|').Append(csoc.Version!.Version)
                    .Append('\n');
            if (provider.Content.Subservice && provider.Content.BoundaryTreatment == "carve_out" &&
                providerCsocs.Length == 0)
            {
                gaps.Add(new ReadinessGapView(GapIdFor(programId, ProviderCsocLinked,
                        provider.ProviderId.ToString()), "carved_out_provider_without_csoc",
                    provider.ProviderId.ToString(), ProviderCsocLinked,
                    $"Carved-out subservice provider {provider.Content.Name} has no effective, applicable CSOC linked to it at the as-of time.",
                    [providerReference]));
            }
        }
    }

    static void EvaluateAccessReviewScopes(Uuid programId, DateTimeOffset asOf,
        ReadinessSourceSet sources, List<ReadinessInputView> inputs,
        List<ReadinessGapView> gaps, StringBuilder fingerprint)
    {
        var scopes = sources.AccessReviewScopes
            .OrderBy(static input => input.SystemInstanceId.ToString(), StringComparer.Ordinal)
            .ToArray();
        inputs.Add(new ReadinessInputView("applications_access_review_scope", "assessed",
            scopes.Length,
            "Effective access-review scope decisions for system instances in the as-of program boundary."));

        foreach (var input in scopes)
        {
            var instanceReference = new ReadinessSourceReference("system_instance",
                input.SystemInstanceId, null);
            var scope = input.Scope;
            var decisions = (scope?.Decisions ?? [])
                .Where(decision => scope is not null &&
                                   decision.TenantId == scope.TenantId &&
                                   decision.ApplicationId == scope.ApplicationId &&
                                   decision.SystemInstanceId == input.SystemInstanceId &&
                                   decision.SystemInstanceRevision > 0 &&
                                   decision.DecidedAt <= asOf && decision.EffectiveFrom <= asOf)
                .OrderBy(static decision => decision.Sequence)
                .ToArray();
            var effective = decisions.LastOrDefault();
            foreach (var decision in decisions)
                fingerprint.Append("access-review-scope|").Append(input.SystemInstanceId)
                    .Append('|').Append(decision.DecisionId).Append('|').Append(decision.Sequence)
                    .Append('|').Append(decision.SystemInstanceRevision).Append('|')
                    .Append(decision.Decision).Append('|')
                    .Append(decision.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture))
                    .Append('|').Append(decision.ReviewBy?.ToString("O", CultureInfo.InvariantCulture))
                    .Append('|').Append(decision.DecidedAt.ToString("O", CultureInfo.InvariantCulture))
                    .Append('\n');

            if (effective is null || effective.Decision is not ("included" or "excluded"))
            {
                gaps.Add(new ReadinessGapView(GapIdFor(programId, AccessReviewScopeResolved,
                        input.SystemInstanceId.ToString()), "access_review_scope_unresolved",
                    input.SystemInstanceId.ToString(), AccessReviewScopeResolved,
                    "The in-boundary system instance has no effective access-review scope decision recorded by the as-of time.",
                    [instanceReference]));
                continue;
            }

            var decisionReference = new ReadinessSourceReference("access_review_scope_decision",
                effective.DecisionId, effective.Sequence.ToString(CultureInfo.InvariantCulture));
            if (effective.ReviewBy is { } reviewBy && reviewBy <= asOf)
                gaps.Add(new ReadinessGapView(GapIdFor(programId, AccessReviewScopeCurrent,
                        input.SystemInstanceId.ToString()), "access_review_scope_overdue",
                    input.SystemInstanceId.ToString(), AccessReviewScopeCurrent,
                    $"The effective access-review scope decision for system instance {input.SystemInstanceId} was due for review by {reviewBy:O}.",
                    [instanceReference, decisionReference]));
        }
    }

    static bool IsApplicableProviderCsoc(CommitmentVersionView? version) =>
        version is not null && version.Kind == "subservice_responsibility" &&
        version.ProviderId is not null && version.Applicability == "applicable" &&
        version.Interpretation == "supported";

    static void EvaluateTechnologyInventory(Uuid programId, DateTimeOffset asOf,
        ReadinessSourceSet sources, List<ReadinessInputView> inputs,
        List<ReadinessGapView> gaps, StringBuilder fingerprint)
    {
        var records = sources.TechnologyInventory
            .OrderBy(static input => input.SubjectType, StringComparer.Ordinal)
            .ThenBy(static input => input.RecordId.ToString(), StringComparer.Ordinal)
            .ToArray();
        inputs.Add(new ReadinessInputView("technology_inventory", "assessed", records.Length,
            "As-of revisions for technology records referenced by the approved program boundary."));

        foreach (var record in records)
        {
            fingerprint.Append("technology-inventory|").Append(record.SubjectType).Append('|')
                .Append(record.RecordId).Append('|').Append(record.Revision).Append('|')
                .Append(record.Lifecycle).Append('|')
                .Append(record.LastChangedAt?.ToString("O", CultureInfo.InvariantCulture))
                .Append('\n');
            var source = new ReadinessSourceReference(record.SubjectType, record.RecordId,
                record.Revision?.ToString(CultureInfo.InvariantCulture));
            if (record.Revision is null)
                gaps.Add(new ReadinessGapView(GapIdFor(programId,
                        TechnologyInventoryRevisionAt,
                        $"{record.SubjectType}|{record.RecordId}"),
                    "technology_inventory_revision_missing_at_as_of",
                    record.RecordId.ToString(), TechnologyInventoryRevisionAt,
                    $"No {record.SubjectType} revision was recorded by the as-of time for a record referenced by the program boundary.",
                    [source]));
            else if (record.Lifecycle != TechnologyInventoryRules.Active)
                gaps.Add(new ReadinessGapView(GapIdFor(programId,
                        TechnologyInventoryActive,
                        $"{record.SubjectType}|{record.RecordId}"),
                    "technology_inventory_record_inactive_at_as_of",
                    record.RecordId.ToString(), TechnologyInventoryActive,
                    $"The {record.SubjectType} record referenced by the program boundary was not active at the as-of time.",
                    [source]));
        }

        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);
        var hasApprovedBoundary = sources.Boundaries.Any(boundary =>
            BoundaryVersionAt(boundary, asOf, asOfDate) is not null);
        if (records.Length == 0 && hasApprovedBoundary)
            gaps.Add(new ReadinessGapView(GapIdFor(programId, TechnologyInventoryPresent,
                    "technology_inventory"), "technology_inventory_missing",
                "technology_inventory", TechnologyInventoryPresent,
                "The approved program boundary references no technology inventory records.", []));
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
