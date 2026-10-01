using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Serves coverage and mapping list reads from their Fitz projections only after both have
///     reached every source event of the tenant; otherwise the read is a transient conflict.
/// </summary>
public sealed class CriteriaCoverageReadConsistency(IControlMappingDirectoryReader mappings,
    ICriterionApplicabilityDirectoryReader applicability, IDomainEventReader events)
{
    const int PageSize = 200;

    public async ValueTask<Result<IReadOnlyList<ControlCriterionMappingView>>> ReadMappingsAsync(
        Uuid tenantId, Uuid programId, CancellationToken ct)
    {
        var lag = await EnsureCaughtUpAsync(tenantId,
            await mappings.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false),
            FitzControlMappingDirectoryV1.SourceArea, "control mapping", ct).ConfigureAwait(false);
        if (!lag.IsSuccess)
            return Result<IReadOnlyList<ControlCriterionMappingView>>.Failure(lag.Error);
        var items = new List<ControlCriterionMappingView>();
        string? cursor = null;
        do
        {
            var page = await mappings.ListProgramAsync(tenantId, programId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Scoped(items, tenantId, programId, static item => (item.TenantId, item.ProgramId));
    }

    public async ValueTask<Result<IReadOnlyList<CriterionApplicabilityView>>> ReadDecisionsAsync(
        Uuid tenantId, Uuid programId, CancellationToken ct)
    {
        var lag = await EnsureCaughtUpAsync(tenantId,
            await applicability.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false),
            FitzCriterionApplicabilityDirectoryV1.SourceArea, "criterion applicability", ct)
            .ConfigureAwait(false);
        if (!lag.IsSuccess)
            return Result<IReadOnlyList<CriterionApplicabilityView>>.Failure(lag.Error);
        var items = new List<CriterionApplicabilityView>();
        string? cursor = null;
        do
        {
            var page = await applicability.ListProgramAsync(tenantId, programId, PageSize,
                cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Scoped(items, tenantId, programId, static item => (item.TenantId, item.ProgramId));
    }

    async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, ProjectionCheckpoint checkpoint,
        string area, string record, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), area), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The {record} projection has not reached the source.", isTransient: true))
            : Result.Success;
    }

    static Result<IReadOnlyList<T>> Scoped<T>(List<T> items, Uuid tenantId, Uuid programId,
        Func<T, (Uuid TenantId, Uuid ProgramId)> scope) =>
        items.Any(item => scope(item) != (tenantId, programId))
            ? Result<IReadOnlyList<T>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The coverage projection has an invalid scope."))
            : Result<IReadOnlyList<T>>.Success(items);
}
