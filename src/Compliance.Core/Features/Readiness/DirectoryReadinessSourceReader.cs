using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Snapshots;
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
    ISnapshotDirectoryReader snapshots)
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
            TruncatedFamilies = truncated,
        };
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
}
