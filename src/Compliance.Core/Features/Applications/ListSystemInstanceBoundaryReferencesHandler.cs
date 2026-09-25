using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListSystemInstanceBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
    : IRequestHandler<ListSystemInstanceBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListSystemInstanceBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance boundary reference list limit must be between 1 and 200."));
        var application = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (!application.HasInstance(request.SystemInstanceId))
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, "system_instance",
                    request.SystemInstanceId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.SubjectType != "system_instance" ||
                                      item.GovernedRecordId != request.SystemInstanceId)
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."))
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }
}
