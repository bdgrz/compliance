using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ReviseBoundaryDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, IBoundaryReferenceValidator references, TimeProvider clock)
    : IRequestHandler<ReviseBoundaryDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseBoundaryDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var boundary = await reader.HydrateAsync(new SystemBoundary(request.TenantId,
            request.BoundaryId), ct).ConfigureAwait(false);
        if (!boundary.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."));
        var validation = await references.ValidateAsync(request.TenantId, boundary.ProgramId,
                request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => CommandFailureRequestAdapter.ToOutcome(boundary.Revise(
                request.DraftVersionId, request.ExpectedRevision, request.Content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
