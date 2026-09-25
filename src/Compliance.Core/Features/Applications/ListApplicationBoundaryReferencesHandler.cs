using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
    : IRequestHandler<ListApplicationBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListApplicationBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application boundary reference list limit must be between 1 and 200."));
        var application = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (!application.IsCreated)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, "application",
                    request.ApplicationId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.SubjectType != "application" ||
                                      item.GovernedRecordId != request.ApplicationId)
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."))
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }
}
