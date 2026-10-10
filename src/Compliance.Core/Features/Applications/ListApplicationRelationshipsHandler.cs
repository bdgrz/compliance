using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ListApplicationRelationshipsHandler(IAggregateReader reader,
    IApplicationRelationshipDirectory directory,
    ApplicationRelationshipReadConsistency consistency,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListApplicationRelationships, Page<ApplicationRelationshipView>>
{
    public async ValueTask<Result<Page<ApplicationRelationshipView>>> HandleAsync(
        IRequestContext<ListApplicationRelationships> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 || request.Direction is not ("outgoing" or "incoming"))
            return Result<Page<ApplicationRelationshipView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The relationship direction must be outgoing or incoming and limit must be between 1 and 200."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<Page<ApplicationRelationshipView>>.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "A personal member identity is required."));

        var application = await reader.HydrateApplicationAsync(request.TenantId,
            request.ApplicationId, ct).ConfigureAwait(false);
        if (!application.IsCreated || application.IsRetired ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<Page<ApplicationRelationshipView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));

        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ApplicationRelationshipView>>.Failure(fence.Error);
        Page<ApplicationRelationshipView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.ApplicationId,
                request.Direction, request.Limit, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationRelationshipView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application relationship cursor is invalid."));
        }

        var visible = new List<ApplicationRelationshipView>(page.Items.Count);
        var omittedHiddenRelationship = false;
        foreach (var relationship in page.Items)
        {
            var counterpartId = request.Direction == "outgoing"
                ? relationship.TargetApplicationId
                : relationship.SourceApplicationId;
            if (relationship.TenantId != request.TenantId ||
                request.Direction == "outgoing" && relationship.SourceApplicationId != request.ApplicationId ||
                request.Direction == "incoming" && relationship.TargetApplicationId != request.ApplicationId)
                return Result<Page<ApplicationRelationshipView>>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The application was not found."));
            var counterpart = await reader.HydrateApplicationAsync(request.TenantId,
                counterpartId, ct).ConfigureAwait(false);
            if (!counterpart.IsCreated || counterpart.IsRetired ||
                !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                    counterpartId, ct).ConfigureAwait(false))
            {
                omittedHiddenRelationship = true;
                continue;
            }
            visible.Add(relationship);
        }

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<Page<ApplicationRelationshipView>>.Failure(confirmed.Error);
        return Result<Page<ApplicationRelationshipView>>.Success(new Page<ApplicationRelationshipView>(
            visible, omittedHiddenRelationship ? null : page.NextCursor));
    }
}
