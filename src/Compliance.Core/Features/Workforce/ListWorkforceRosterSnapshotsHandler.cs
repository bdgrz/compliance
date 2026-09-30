using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListWorkforceRosterSnapshotsHandler(IPopulationSnapshotDirectoryReader directory)
    : IRequestHandler<ListWorkforceRosterSnapshots, Page<PopulationSnapshotSummary>>
{
    public async ValueTask<Result<Page<PopulationSnapshotSummary>>> HandleAsync(
        IRequestContext<ListWorkforceRosterSnapshots> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PopulationSnapshotSummary>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The roster snapshot list limit must be between 1 and 200."));
        Page<PopulationSnapshotSummary> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, WorkforceRosterSnapshotContent.Kind,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PopulationSnapshotSummary>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The roster snapshot cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.Kind != WorkforceRosterSnapshotContent.Kind)
            ? Result<Page<PopulationSnapshotSummary>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The roster snapshots were not found."))
            : Result<Page<PopulationSnapshotSummary>>.Success(page);
    }
}
