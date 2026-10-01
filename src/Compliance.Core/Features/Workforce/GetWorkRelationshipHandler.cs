using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Returns one work relationship, disclosing the restricted manager chain only to an actor with
///     the <c>workforce.manager_chain</c> field grant (M0-D06).
/// </summary>
public sealed class GetWorkRelationshipHandler(WorkRelationshipReadConsistency consistency,
    IPermissionAuthorizer permissions) : IRequestHandler<GetWorkRelationship, WorkRelationshipView>
{
    public async ValueTask<Result<WorkRelationshipView>> HandleAsync(
        IRequestContext<GetWorkRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.RelationshipId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        var personalDetails = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        return Result<WorkRelationshipView>.Success(view.Value with
        {
            ManagerPersonId = managerChain.CanRead ? view.Value.ManagerPersonId : null,
            EmploymentStatusReason = personalDetails.CanRead
                ? view.Value.EmploymentStatusReason
                : null,
            RestrictedFieldsRedacted = view.Value.RestrictedFieldsRedacted ||
                !managerChain.CanRead || !personalDetails.CanRead,
        });
    }
}
