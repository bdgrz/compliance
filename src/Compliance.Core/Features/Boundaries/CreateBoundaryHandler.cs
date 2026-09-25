using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class CreateBoundaryHandler(IAggregateExecutor executor,
    IAggregateReader reader, IBoundaryReferenceValidator references,
    TimeProvider clock)
    : IRequestHandler<CreateBoundary, BoundaryRegistration>
{
    public async ValueTask<Result<BoundaryRegistration>> HandleAsync(
        IRequestContext<CreateBoundary> context, CancellationToken ct)
    {
        var request = context.Request;
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var validation = await references.ValidateAsync(request.TenantId, request.ProgramId,
                request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<BoundaryRegistration>.Failure(validation.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var boundaryId = context.RequestId;
        var draftVersionId = Uuid.CreateVersion5(boundaryId, "draft-1");
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, boundaryId),
            boundary => AggregateOutcome.CommitOnSuccess(boundary.Create(request.ProgramId,
                draftVersionId, request.Content, RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
