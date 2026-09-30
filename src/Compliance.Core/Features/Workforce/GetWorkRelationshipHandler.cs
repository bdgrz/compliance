using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class GetWorkRelationshipHandler(WorkRelationshipReadConsistency consistency)
    : IRequestHandler<GetWorkRelationship, WorkRelationshipView>
{
    public ValueTask<Result<WorkRelationshipView>> HandleAsync(IRequestContext<GetWorkRelationship> context,
        CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.RelationshipId,
            context.Request.MinimumRevision, ct);
}
