using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class ListResponsibilitiesHandler(IResponsibilitySetDirectory directory,
    IResponsibilityScopeValidator scopeValidator)
    : IRequestHandler<ListResponsibilities, ResponsibilitySetView>
{
    public async ValueTask<Result<ResponsibilitySetView>> HandleAsync(
        IRequestContext<ListResponsibilities> context, CancellationToken ct)
    {
        var request = context.Request;
        var validation = await scopeValidator.ValidateAsync(request.TenantId, request.Scope, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<ResponsibilitySetView>.Failure(validation.Error);
        if (request.MinimumRevision is < 1)
            return Result<ResponsibilitySetView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum responsibility revision must be positive."));
        var view = await directory.GetAsync(request.TenantId, request.Scope, ct)
            .ConfigureAwait(false);
        if (view is null)
            return Result<ResponsibilitySetView>.Success(new ResponsibilitySetView(
                request.TenantId, ResponsibilitySet.IdFor(request.TenantId, request.Scope),
                request.Scope, 0, []));
        if (view.TenantId != request.TenantId || view.Scope != request.Scope ||
            view.SetId != ResponsibilitySet.IdFor(request.TenantId, request.Scope))
            return Result<ResponsibilitySetView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The responsibility projection has an invalid scope."));
        return request.MinimumRevision is { } minimum && view.Revision < minimum
            ? Result<ResponsibilitySetView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The responsibility projection has not reached revision {minimum}."))
            : Result<ResponsibilitySetView>.Success(view);
    }
}
