using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

sealed class FitzCriteriaTextOverlayReader(ICriteriaTextOverlayDirectoryReader directory,
    CriteriaTextOverlayReadConsistency consistency) : ICriteriaTextOverlayReader
{
    public async ValueTask<Result<Criterion>> ApplyAsync(Uuid tenantId, Criterion entry,
        bool isExport, CancellationToken ct)
    {
        var applied = await ApplyPageAsync(tenantId, [entry], isExport, ct).ConfigureAwait(false);
        return applied.IsSuccess
            ? Result<Criterion>.Success(applied.Value[0])
            : Result<Criterion>.Failure(applied.Error);
    }

    public async ValueTask<Result<IReadOnlyList<Criterion>>> ApplyPageAsync(Uuid tenantId,
        IReadOnlyList<Criterion> entries, bool isExport, CancellationToken ct)
    {
        if (entries.Count == 0)
            return Result<IReadOnlyList<Criterion>>.Success(entries);
        var editionId = entries[0].EditionId;
        if (entries.Any(entry => entry.EditionId != editionId))
            return Result<IReadOnlyList<Criterion>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A criteria overlay page must contain one edition."));

        var captured = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<IReadOnlyList<Criterion>>.Failure(captured.Error);
        var overlays = await directory.GetManyAsync(tenantId, editionId,
            entries.Select(static entry => entry.Identifier).Distinct(StringComparer.Ordinal)
                .ToArray(), ct).ConfigureAwait(false);
        if (!overlays.IsSuccess)
            return Result<IReadOnlyList<Criterion>>.Failure(overlays.Error);
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            captured.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<IReadOnlyList<Criterion>>.Failure(confirmed.Error);

        var result = entries.Select(entry => overlays.Value.TryGetValue(entry.Identifier,
                out var current)
            ? CriteriaTextOverlayPolicy.Apply(entry, current, current, isExport)
            : entry).ToArray();
        return Result<IReadOnlyList<Criterion>>.Success(result);
    }
}
