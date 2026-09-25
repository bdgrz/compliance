using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ProposeBoundarySuccessorHandler(IAggregateExecutor executor,
    IAggregateReader reader, IBoundaryReferenceValidator references, TimeProvider clock)
    : IRequestHandler<ProposeBoundarySuccessor, BoundaryRegistration>
{
    public async ValueTask<Result<BoundaryRegistration>> HandleAsync(
        IRequestContext<ProposeBoundarySuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        var boundary = await reader.HydrateAsync(new SystemBoundary(request.TenantId,
            request.BoundaryId), ct).ConfigureAwait(false);
        if (!boundary.IsCreated)
            return Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The boundary was not found."));
        var validation = await references.ValidateAsync(request.TenantId, boundary.ProgramId,
                request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<BoundaryRegistration>.Failure(validation.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var draftVersionId = Uuid.CreateVersion5(context.RequestId, "draft");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary => CommandFailureRequestAdapter.ToOutcome(
                boundary.ProposeSuccessor(request.ExpectedApprovedVersionId, draftVersionId,
                    request.Content, RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow()),
                new BoundaryRegistration(request.BoundaryId, draftVersionId)),
            context, ct).ConfigureAwait(false);
    }
}
