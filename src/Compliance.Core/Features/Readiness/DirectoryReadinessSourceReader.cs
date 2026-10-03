using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Reads each source family through its canonical program directory with the history the rules
///     need to resolve records as of the assessment time. Each family scan is bounded by
///     <see cref="MaximumRecordsPerFamily" />; a family that exceeds it is reported as truncated so
///     the rules record a gap instead of silently assessing a partial population. Each record's
///     history is read exactly once per run; the directories expose no batched history read.
/// </summary>
sealed class DirectoryReadinessSourceReader(IBoundaryDirectoryReader boundaries,
    ICommitmentDraftDirectoryReader commitments, IRiskDraftDirectoryReader risks,
    IRiskDraftHistoryDirectoryReader riskHistory, IRiskEvaluationDirectoryReader evaluations,
    ISnapshotDirectoryReader snapshots, IProviderReader providers, IAssuranceReader assurance,
    IAccessReviewScopeDirectoryReader accessReviewScopes,
    ITechnologyInventoryReader technology, IInventoryRegisterReader inventoryRegister)
    : IReadinessSourceReader
{
    public const int MaximumRecordsPerFamily = 500;
    const int PageSize = 200;

    public async ValueTask<ReadinessSourceSet> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        var truncated = new List<string>();
        var boundaryViews = await ScanAsync("boundaries", truncated, (cursor, token) =>
            boundaries.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var commitmentViews = await ScanAsync("commitments", truncated, (cursor, token) =>
            commitments.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var riskViews = await ScanAsync("risks", truncated, (cursor, token) =>
            risks.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var snapshotViews = await ScanAsync("population_snapshots", truncated, (cursor, token) =>
            snapshots.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var boundaryInputs = new List<ReadinessBoundaryInput>(boundaryViews.Count);
        foreach (var boundary in boundaryViews)
        {
            var versions = await ReadAllAsync(async (cursor, token) =>
                    await boundaries.ListVersionsAsync(tenantId, boundary.BoundaryId, PageSize,
                        cursor, token).ConfigureAwait(false) ?? new Page<BoundaryVersionView>([], null),
                ct).ConfigureAwait(false);
            var decisions = await ReadAllAsync(async (cursor, token) =>
                    await boundaries.ListDecisionsAsync(tenantId, boundary.BoundaryId, PageSize,
                        cursor, token).ConfigureAwait(false) ?? new Page<BoundaryDecisionView>([], null),
                ct).ConfigureAwait(false);
            boundaryInputs.Add(new ReadinessBoundaryInput(boundary.BoundaryId, versions,
                decisions));
        }

        var scopedSubjects = SubjectsAt(boundaryInputs, asOf);
        var accessReviewScopeInputs = await ReadAccessReviewScopesAsync(tenantId,
            scopedSubjects, truncated, ct).ConfigureAwait(false);
        var technologyInputs = await ReadTechnologyInventoryAsync(tenantId, asOf,
            scopedSubjects, truncated, ct).ConfigureAwait(false);
        var providerInputs = await ReadProvidersAsync(tenantId, programId, asOf,
            scopedSubjects, truncated, ct).ConfigureAwait(false);

        var commitmentInputs = new List<ReadinessCommitmentInput>(commitmentViews.Count);
        foreach (var commitment in commitmentViews)
        {
            var created = await commitments.GetRevisionAsync(tenantId, commitment.DraftId, 1, ct)
                .ConfigureAwait(false);
            var versions = await ReadAllAsync((cursor, token) =>
                commitments.ListVersionsAsync(tenantId, commitment.DraftId, PageSize, cursor,
                    token), ct).ConfigureAwait(false);
            commitmentInputs.Add(new ReadinessCommitmentInput(commitment.DraftId,
                commitment.Identifier, created?.ChangedAt ?? commitment.LastChangedAt, versions));
        }

        var riskInputs = new List<ReadinessRiskInput>(riskViews.Count);
        foreach (var risk in riskViews)
        {
            var revisions = await ReadAllAsync((cursor, token) =>
                riskHistory.ListRevisionsAsync(tenantId, risk.RiskId, PageSize, cursor, token),
                ct).ConfigureAwait(false);
            var createdAt = revisions.Count == 0
                ? risk.LastChangedAt
                : revisions.Min(static revision => revision.ChangedAt);
            var revisionAt = revisions.Where(revision => revision.ChangedAt <= asOf)
                .Select(static revision => revision.Revision).DefaultIfEmpty(0).Max();
            var evaluation = await evaluations.GetAsync(tenantId, risk.RiskId, ct)
                .ConfigureAwait(false);
            riskInputs.Add(new ReadinessRiskInput(risk.RiskId, risk.Identifier, createdAt,
                revisionAt, EvaluationStatusAt(evaluation, asOf)));
        }

        return new ReadinessSourceSet(boundaryInputs, commitmentInputs, riskInputs,
            snapshotViews)
        {
            AccessReviewScopes = accessReviewScopeInputs,
            Providers = providerInputs,
            TechnologyInventory = technologyInputs,
            TruncatedFamilies = truncated,
        };
    }

    async ValueTask<IReadOnlyList<ReadinessTechnologyInventoryInput>> ReadTechnologyInventoryAsync(
        Uuid tenantId, DateTimeOffset asOf,
        HashSet<(string SubjectType, Uuid RecordId)> scopedSubjects,
        List<string> truncated, CancellationToken ct)
    {
        var targets = scopedSubjects
            .Where(static subject => subject.SubjectType is "component" or "information" or
                "data_flow" or "location" or "process")
            .GroupBy(static subject => subject.SubjectType, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                static group => group.Select(static subject => subject.RecordId)
                    .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var inputs = new List<ReadinessTechnologyInventoryInput>();
        var recordReads = 0;
        var historyReads = 0;
        await AddRevisionsAsync("component", targets.GetValueOrDefault("component", []),
            (id, cursor, token) => technology.ListComponentRevisionsAsync(tenantId, id,
                PageSize, cursor, token), static view => view.TenantId,
            static view => view.ComponentId, static view => view.Revision,
            static view => view.LastChangedAt, static view => view.Content.Lifecycle)
            .ConfigureAwait(false);
        await AddRevisionsAsync("information", targets.GetValueOrDefault("information", []),
            (id, cursor, token) => technology.ListAssetRevisionsAsync(tenantId, id,
                PageSize, cursor, token), static view => view.TenantId,
            static view => view.InformationAssetId, static view => view.Revision,
            static view => view.LastChangedAt, static view => view.Content.Lifecycle)
            .ConfigureAwait(false);
        await AddRevisionsAsync("data_flow", targets.GetValueOrDefault("data_flow", []),
            (id, cursor, token) => technology.ListFlowRevisionsAsync(tenantId, id,
                PageSize, cursor, token), static view => view.TenantId,
            static view => view.DataFlowId, static view => view.Revision,
            static view => view.LastChangedAt, static view => view.Content.Lifecycle)
            .ConfigureAwait(false);
        await AddRevisionsAsync("location", targets.GetValueOrDefault("location", []),
            (id, cursor, token) => inventoryRegister.ListLocationRevisionsAsync(tenantId, id,
                PageSize, cursor, token), static view => view.TenantId,
            static view => view.LocationId, static view => view.Revision,
            static view => view.LastChangedAt, static view => view.Content.Lifecycle)
            .ConfigureAwait(false);
        await AddRevisionsAsync("process", targets.GetValueOrDefault("process", []),
            (id, cursor, token) => inventoryRegister.ListOperationalProcessRevisionsAsync(
                tenantId, id, PageSize, cursor, token), static view => view.TenantId,
            static view => view.OperationalProcessId, static view => view.Revision,
            static view => view.LastChangedAt, static view => view.Content.Lifecycle)
            .ConfigureAwait(false);
        return inputs;

        async ValueTask AddRevisionsAsync<T>(string subjectType, IReadOnlyList<Uuid> recordIds,
            Func<Uuid, string?, CancellationToken, ValueTask<Page<T>>> read,
            Func<T, Uuid> getTenantId, Func<T, Uuid> getRecordId,
            Func<T, long> getRevision, Func<T, DateTimeOffset> getChangedAt,
            Func<T, string> getLifecycle) where T : class
        {
            if (recordIds.Count > MaximumRecordsPerFamily &&
                !truncated.Contains("technology_inventory", StringComparer.Ordinal))
                truncated.Add("technology_inventory");
            foreach (var recordId in recordIds.Take(MaximumRecordsPerFamily))
            {
                if (recordReads >= MaximumRecordsPerFamily)
                {
                    if (!truncated.Contains("technology_inventory", StringComparer.Ordinal))
                        truncated.Add("technology_inventory");
                    return;
                }
                var remainingHistory = MaximumRecordsPerFamily - historyReads;
                if (remainingHistory <= 0)
                {
                    if (!truncated.Contains("technology_inventory", StringComparer.Ordinal))
                        truncated.Add("technology_inventory");
                    return;
                }
                var revisions = await ReadBoundedAsync((cursor, token) =>
                        read(recordId, cursor, token), remainingHistory, ct)
                    .ConfigureAwait(false);
                historyReads += revisions.Items.Count;
                if (revisions.Truncated &&
                    !truncated.Contains("technology_inventory", StringComparer.Ordinal))
                    truncated.Add("technology_inventory");
                var selected = revisions.Items
                    .Where(view => getTenantId(view) == tenantId &&
                                   getRecordId(view) == recordId &&
                                   getChangedAt(view) <= asOf)
                    .MaxBy(getRevision);
                inputs.Add(selected is null
                    ? new ReadinessTechnologyInventoryInput(subjectType, recordId,
                        null, null, null)
                    : new ReadinessTechnologyInventoryInput(subjectType, recordId,
                        getRevision(selected), getLifecycle(selected), getChangedAt(selected)));
                recordReads++;
                if (revisions.Truncated)
                    return;
            }
        }
    }

    async ValueTask<IReadOnlyList<ReadinessAccessReviewScopeInput>> ReadAccessReviewScopesAsync(
        Uuid tenantId, HashSet<(string SubjectType, Uuid RecordId)> scopedSubjects,
        List<string> truncated, CancellationToken ct)
    {
        var instanceIds = scopedSubjects
            .Where(static subject => subject.SubjectType == "system_instance")
            .Select(static subject => subject.RecordId)
            .Distinct()
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (instanceIds.Length > MaximumRecordsPerFamily &&
            !truncated.Contains("applications_access_review_scope", StringComparer.Ordinal))
            truncated.Add("applications_access_review_scope");
        var inputs = new List<ReadinessAccessReviewScopeInput>(
            Math.Min(instanceIds.Length, MaximumRecordsPerFamily));
        foreach (var instanceId in instanceIds.Take(MaximumRecordsPerFamily))
        {
            var record = await accessReviewScopes.GetAsync(tenantId, instanceId, ct)
                .ConfigureAwait(false);
            if (record is not null && (record.TenantId != tenantId ||
                                       record.SystemInstanceId != instanceId))
                record = null;
            inputs.Add(new ReadinessAccessReviewScopeInput(instanceId, record));
        }
        return inputs;
    }

    async ValueTask<IReadOnlyList<ReadinessProviderInput>> ReadProvidersAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset asOf, HashSet<(string SubjectType, Uuid RecordId)> scopedSubjects,
        List<string> truncated, CancellationToken ct)
    {
        var currentProviders = await ScanAsync("providers", truncated, (cursor, token) =>
            providers.ListAsync(tenantId, PageSize, cursor, token), ct).ConfigureAwait(false);
        var inputs = new List<ReadinessProviderInput>();
        var historyReads = 0;
        var historyTruncated = false;
        foreach (var current in currentProviders)
        {
            var provider = current;
            while (provider.RecordedAt > asOf && provider.Revision > 1)
            {
                if (historyReads >= MaximumRecordsPerFamily)
                {
                    historyTruncated = true;
                    break;
                }
                var previous = await providers.GetRevisionAsync(tenantId, provider.ProviderId,
                    provider.Revision - 1, ct).ConfigureAwait(false);
                if (previous is null)
                {
                    historyTruncated = true;
                    break;
                }
                provider = previous;
                historyReads++;
            }
            if (provider.RecordedAt > asOf)
            {
                if (provider.Revision > 1)
                    historyTruncated = true;
                continue;
            }

            var inProgram = (provider.Content.Dependencies ?? []).Any(dependency =>
                dependency.SubjectId is not null &&
                dependency.EffectiveFrom <= asOf &&
                (dependency.EffectiveUntilExclusive is not { } until || asOf < until) &&
                (dependency.SubjectKind == "client_service" && dependency.ProgramId == programId ||
                 dependency.SubjectKind == "system_instance" &&
                 scopedSubjects.Contains(("system_instance", dependency.SubjectId.Value))));
            if (!inProgram)
                continue;

            var reviews = await ReadBoundedAsync((cursor, token) =>
                    assurance.ListReviewsAsync(tenantId, provider.ProviderId, PageSize, cursor, token),
                ProviderAssuranceRegister.MaximumRecordsPerProvider, ct).ConfigureAwait(false);
            if (reviews.Truncated)
            {
                if (!truncated.Contains("providers", StringComparer.Ordinal))
                    truncated.Add("providers");
            }
            var retainedReviews = reviews.Items
                .Where(review => review.RecordedAt <= asOf).ToArray();
            var reports = new Dictionary<(Uuid ReportId, long Revision), AssuranceReportView>();
            foreach (var review in retainedReviews)
            {
                if (review.Content.AssuranceReportId is not { } reportId ||
                    review.AssuranceReportRevision is not { } revision)
                    continue;
                var report = await assurance.GetReportRevisionAsync(tenantId, reportId,
                    revision, ct).ConfigureAwait(false);
                if (report is { } retained && retained.ProviderId == provider.ProviderId &&
                    retained.RecordedAt <= asOf)
                    reports[(reportId, revision)] = retained;
            }
            inputs.Add(new ReadinessProviderInput(provider,
                reports.Values.ToArray(), retainedReviews));
        }
        if (historyTruncated && !truncated.Contains("providers", StringComparer.Ordinal))
            truncated.Add("providers");
        return inputs;
    }

    static HashSet<(string SubjectType, Uuid RecordId)> SubjectsAt(
        IReadOnlyList<ReadinessBoundaryInput> boundaries,
        DateTimeOffset asOf)
    {
        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);
        var subjects = new HashSet<(string SubjectType, Uuid RecordId)>();
        foreach (var boundary in boundaries)
        {
            var approvedBy = boundary.Decisions
                .Where(decision => decision.Outcome == "approve" && decision.DecidedAt <= asOf)
                .Select(static decision => decision.VersionId)
                .ToHashSet();
            var version = boundary.ApprovedVersions
                .Where(item => approvedBy.Contains(item.VersionId) &&
                               item.EffectiveFrom is { } from && from <= asOfDate)
                .MaxBy(static item => item.EffectiveFrom);
            if (version is null)
                continue;
            foreach (var entry in version.Content.Entries.Where(static entry =>
                         entry.Kind == "inclusion" && !entry.Unresolved &&
                         entry.SubjectType is "service" or "system_instance" or "component" or
                             "information" or "data_flow" or "location" or "process" &&
                         entry.GovernedRecordId is not null))
                subjects.Add((entry.SubjectType, entry.GovernedRecordId!.Value));
        }
        return subjects;
    }

    /// <summary>The risk status computed only from assessments, treatment, and acceptances recorded by the as-of time.</summary>
    static string EvaluationStatusAt(RiskEvaluationView? evaluation, DateTimeOffset asOf)
    {
        if (evaluation is null)
            return "unassessed";
        var atTime = evaluation with
        {
            Assessments = evaluation.Assessments.Where(a => a.AssessedAt <= asOf).ToArray(),
            Acceptances = evaluation.Acceptances.Where(a => a.AcceptedAt <= asOf).ToArray(),
            Treatment = evaluation.Treatment is { } treatment && treatment.ChosenAt <= asOf
                ? treatment
                : null,
        };
        return RiskEvaluationStatus.AsOf(atTime, asOf).Status;
    }

    static async ValueTask<IReadOnlyList<T>> ScanAsync<T>(string family, List<string> truncated,
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
            if (items.Count > MaximumRecordsPerFamily)
            {
                truncated.Add(family);
                return items.Take(MaximumRecordsPerFamily).ToArray();
            }
        } while (cursor is not null);
        return items;
    }

    static async ValueTask<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }

    static async ValueTask<(IReadOnlyList<T> Items, bool Truncated)> ReadBoundedAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, int maximum,
        CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
            if (items.Count > maximum)
                return (items.Take(maximum).ToArray(), true);
        } while (cursor is not null);
        return (items, false);
    }
}
