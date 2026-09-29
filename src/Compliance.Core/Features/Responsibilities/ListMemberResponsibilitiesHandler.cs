using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class ListMemberResponsibilitiesHandler(IMemberResponsibilityIndex index,
    TimeProvider clock) : IRequestHandler<ListMemberResponsibilities,
        IReadOnlyList<ResponsibilityAssignmentView>>
{
    public async ValueTask<Result<IReadOnlyList<ResponsibilityAssignmentView>>> HandleAsync(
        IRequestContext<ListMemberResponsibilities> context, CancellationToken ct)
    {
        var assignments = await index.GetAsync(context.Request.TenantId,
            RbacIds.Member(context.Request.TenantId, context.Request.UserId), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return Result<IReadOnlyList<ResponsibilityAssignmentView>>.Success(assignments
            .Where(item => item.RevokedAt is null &&
                (item.EffectiveUntil is null || item.EffectiveUntil > now))
            .OrderBy(item => item.Type).ThenBy(item => item.Scope.RecordType, StringComparer.Ordinal)
            .ThenBy(item => item.AssignmentId.ToString(), StringComparer.Ordinal).ToArray());
    }
}
