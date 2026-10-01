using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Reads the boundary reverse index for one inventory record after confirming the record exists
///     in the tenant and the index has caught up; rows for any other tenant or record hide it.
/// </summary>
static class InventoryBoundaryReferenceReader
{
    public static async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> ListAsync(
        bool recordExists, string noun, string subjectType, Uuid tenantId, Uuid recordId,
        int? limit, string? cursor, IApplicationBoundaryReferenceDirectory directory,
        ApplicationBoundaryReferenceReadConsistency consistency, CancellationToken ct)
    {
        var notFound = Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
            RequestErrorKind.NotFound, $"The {noun} was not found."));
        if (!recordExists)
            return notFound;
        var ready = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(tenantId, subjectType, recordId, limit ?? 50,
                cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, $"The {noun} boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != tenantId ||
                                      item.SubjectType != subjectType ||
                                      item.GovernedRecordId != recordId)
            ? notFound
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }

    public static Result<Page<ApplicationBoundaryReferenceView>>? RejectLimit(int? limit, string noun) =>
        limit is < 1 or > 200
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                $"The {noun} boundary reference list limit must be between 1 and 200."))
            : null;
}
