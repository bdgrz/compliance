using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListWorkforceObservationsHandler(IWorkforceObservationDirectoryReader directory,
    WorkforceObservationReadConsistency consistency)
    : IRequestHandler<ListWorkforceObservations, Page<WorkforceObservationView>>
{
    static readonly string[] Kinds = ["joiner", "mover", "leaver"];

    public async ValueTask<Result<Page<WorkforceObservationView>>> HandleAsync(
        IRequestContext<ListWorkforceObservations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Invalid("The workforce observation list limit must be between 1 and 200.");
        if (request.Kind is { } kind && !Kinds.Contains(kind))
            return Invalid("The observation kind must be joiner, mover, or leaver.");
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<WorkforceObservationView>>.Failure(ready.Error);
        Page<WorkforceObservationView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Kind, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Invalid("The workforce observation cursor is invalid.");
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<WorkforceObservationView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The workforce observations were not found."))
            : Result<Page<WorkforceObservationView>>.Success(page);
    }

    static Result<Page<WorkforceObservationView>> Invalid(string message) =>
        Result<Page<WorkforceObservationView>>.Failure(
            new RequestError(RequestErrorKind.Validation, message));
}
