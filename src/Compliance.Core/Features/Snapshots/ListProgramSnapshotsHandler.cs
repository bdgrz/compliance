using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class ListProgramSnapshotsHandler(ISnapshotDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<ListProgramSnapshots, Page<SnapshotView>>
{
    public async ValueTask<Result<Page<SnapshotView>>> HandleAsync(
        IRequestContext<ListProgramSnapshots> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot list limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        Page<SnapshotView> page;
        try
        {
            page = await directory.ListProgramAsync(
                request.TenantId, request.ProgramId, request.Limit ?? 50, request.Cursor,
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProgramId != request.ProgramId || !SnapshotContentIdentity.MatchesView(item)))
            return Result<Page<SnapshotView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "A stored snapshot manifest failed integrity verification."));
        return Result<Page<SnapshotView>>.Success(page);
    }
}
