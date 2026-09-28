using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class PreviewResponsibilityConflictsHandler(IResponsibilitySetDirectory directory,
    ITenantMembershipDirectoryReader memberships, IResponsibilityScopeValidator scopeValidator)
    : IRequestHandler<PreviewResponsibilityConflicts, ResponsibilityConflictPreview>
{
    public async ValueTask<Result<ResponsibilityConflictPreview>> HandleAsync(
        IRequestContext<PreviewResponsibilityConflicts> context, CancellationToken ct)
    {
        var request = context.Request;
        var validation = await scopeValidator.ValidateAsync(request.TenantId, request.Scope, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<ResponsibilityConflictPreview>.Failure(validation.Error);
        var member = await memberships.GetAsync(request.TenantId.ToString(), request.MemberUserId, ct)
            .ConfigureAwait(false);
        if (member is null || member.Affiliation == "firm_staff")
            return Result<ResponsibilityConflictPreview>.Failure(new RequestError(
                RequestErrorKind.Validation, "The responsibility assignee must be an active tenant member."));
        if (request.EffectiveUntil is { } until && until <= request.EffectiveFrom)
            return Result<ResponsibilityConflictPreview>.Failure(new RequestError(
                RequestErrorKind.Validation, "The responsibility interval must end after it starts."));
        if (!ResponsibilityTypeWireName.TryParse(request.Type, out var responsibilityType))
            return Result<ResponsibilityConflictPreview>.Failure(new RequestError(
                RequestErrorKind.Validation, "The responsibility type is invalid."));
        var view = await directory.GetAsync(request.TenantId, request.Scope, ct).ConfigureAwait(false);
        if (view is not null && (view.TenantId != request.TenantId || view.Scope != request.Scope ||
                view.SetId != ResponsibilitySet.IdFor(request.TenantId, request.Scope)))
            return Result<ResponsibilityConflictPreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The responsibility projection has an invalid scope."));
        var memberId = RbacIds.Member(request.TenantId, request.MemberUserId);
        var proposed = new ResponsibilityAssignmentView(request.TenantId, Uuid.Empty, memberId,
            responsibilityType, request.Scope, request.EffectiveFrom, Uuid.Empty, request.EffectiveFrom,
            request.EffectiveUntil, null, Uuid.Empty, []);
        var conflicts = ResponsibilityConflictPolicy.FindConflicts(view?.Assignments ?? [], proposed);
        return Result<ResponsibilityConflictPreview>.Success(new ResponsibilityConflictPreview(
            request.Scope, view?.Revision ?? 0, conflicts));
    }
}
