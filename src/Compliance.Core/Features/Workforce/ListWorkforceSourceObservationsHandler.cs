using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListWorkforceSourceObservationsHandler(IWorkforceSourceDirectoryReader directory,
    WorkforceSourceReadConsistency consistency)
    : IRequestHandler<ListWorkforceSourceObservations, Page<WorkforceSourceView>>
{
    public async ValueTask<Result<Page<WorkforceSourceView>>> HandleAsync(
        IRequestContext<ListWorkforceSourceObservations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 || (request.TargetKind is null) != (request.TargetId is null) ||
            request.TargetId == Uuid.Empty || request.TargetKind is not (null or "person" or "work_relationship" or "service_identity"))
            return Invalid("Use a limit of 1–200 and pair a valid target_kind with target_id.");
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<WorkforceSourceView>>.Failure(ready.Error);
        try
        {
            var page = await directory.ListAsync(request.TenantId, request.TargetKind, request.TargetId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
            if (page.Items.Any(item => item.TenantId != request.TenantId))
                return Result<Page<WorkforceSourceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The source observations were not found."));
            return Result<Page<WorkforceSourceView>>.Success(new Page<WorkforceSourceView>(
                [.. page.Items.Select(item => WorkforceSourceRedaction.Apply(item, false, false))], page.NextCursor));
        }
        catch (KvDirectoryQueryException)
        {
            return Invalid("The source observation cursor is invalid.");
        }
    }

    static Result<Page<WorkforceSourceView>> Invalid(string message) =>
        Result<Page<WorkforceSourceView>>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
