using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListWorkRelationshipsHandler(IWorkRelationshipDirectoryReader directory,
    WorkRelationshipReadConsistency consistency) : IRequestHandler<ListWorkRelationships, Page<WorkRelationshipView>>
{
    public async ValueTask<Result<Page<WorkRelationshipView>>> HandleAsync(
        IRequestContext<ListWorkRelationships> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<WorkRelationshipView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The work relationship list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<WorkRelationshipView>>.Failure(ready.Error);
        Page<WorkRelationshipView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<WorkRelationshipView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The work relationship cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<WorkRelationshipView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The work relationships were not found."))
            : Result<Page<WorkRelationshipView>>.Success(Redact(page));
    }

    // Lists never disclose restricted workforce fields (M0-D06).
    static Page<WorkRelationshipView> Redact(Page<WorkRelationshipView> page) =>
        new([.. page.Items.Select(static item => item with
        {
            ManagerPersonId = null,
            RestrictedFieldsRedacted = true,
        })], page.NextCursor);
}
