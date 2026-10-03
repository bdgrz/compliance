using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

sealed class EmptyCriteriaTextOverlayReader : ICriteriaTextOverlayReader
{
    public ValueTask<Result<Criterion>> ApplyAsync(Uuid tenantId, Criterion entry, bool isExport,
        CancellationToken ct) => ValueTask.FromResult(Result<Criterion>.Success(entry));

    public ValueTask<Result<IReadOnlyList<Criterion>>> ApplyPageAsync(Uuid tenantId,
        IReadOnlyList<Criterion> entries, bool isExport, CancellationToken ct) =>
        ValueTask.FromResult(Result<IReadOnlyList<Criterion>>.Success(entries));
}
