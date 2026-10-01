using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Reads each source family through its canonical program directory, one paged scan per
///     family. Risk evaluations are read once per risk and evaluated as of the assessment time.
/// </summary>
sealed class DirectoryReadinessSourceReader(IBoundaryDirectoryReader boundaries,
    ICommitmentDraftDirectoryReader commitments, IRiskDraftDirectoryReader risks,
    IRiskEvaluationDirectoryReader evaluations, ISnapshotDirectoryReader snapshots)
    : IReadinessSourceReader
{
    const int PageSize = 200;

    public async ValueTask<ReadinessSourceSet> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        var boundaryViews = await ReadAllAsync((cursor, token) =>
            boundaries.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var commitmentViews = await ReadAllAsync((cursor, token) =>
            commitments.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var riskViews = await ReadAllAsync((cursor, token) =>
            risks.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var snapshotViews = await ReadAllAsync((cursor, token) =>
            snapshots.ListProgramAsync(tenantId, programId, PageSize, cursor, token), ct)
            .ConfigureAwait(false);
        var riskInputs = new List<ReadinessRiskInput>(riskViews.Count);
        foreach (var risk in riskViews)
        {
            var evaluation = await evaluations.GetAsync(tenantId, risk.RiskId, ct)
                .ConfigureAwait(false);
            var status = evaluation is null
                ? "unassessed"
                : RiskEvaluationStatus.AsOf(evaluation, asOf).Status;
            riskInputs.Add(new ReadinessRiskInput(risk.RiskId, risk.Identifier, risk.Revision,
                status));
        }
        return new ReadinessSourceSet(boundaryViews, commitmentViews, riskInputs,
            snapshotViews);
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
