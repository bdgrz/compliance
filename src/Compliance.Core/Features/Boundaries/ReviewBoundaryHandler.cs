using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ReviewBoundaryHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock)
    : IRequestHandler<ReviewBoundary>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var waiverId = request.SeparationOfDutiesWaiverId;
        SeparationOfDutiesWaiver? waiver = null;
        if (waiverId is not null)
        {
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId.Value), ct).ConfigureAwait(false);
        }
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.BoundaryId),
            boundary =>
            {
                if (boundary.DraftVersionId != request.DraftVersionId ||
                    boundary.DraftRevision != request.ExpectedRevision)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The boundary draft changed. Reload it before review.")));
                return CommandFailureRequestAdapter.ToOutcome(boundary.Review(request.DraftVersionId,
                    request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                    memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId),
                    clock.GetUtcNow(), waiver));
            },
            context, ct).ConfigureAwait(false);
    }
}
