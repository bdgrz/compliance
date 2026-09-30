using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class GetServiceIdentityHandler(ServiceIdentityReadConsistency consistency,
    ServiceIdentityOwnership ownership) : IRequestHandler<GetServiceIdentity, ServiceIdentityView>
{
    public async ValueTask<Result<ServiceIdentityView>> HandleAsync(
        IRequestContext<GetServiceIdentity> context, CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.ServiceIdentityId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var evaluated = await ownership.EvaluateAsync(request.TenantId, [view.Value], ct)
            .ConfigureAwait(false);
        return evaluated.IsSuccess
            ? Result<ServiceIdentityView>.Success(evaluated.Value[0])
            : Result<ServiceIdentityView>.Failure(evaluated.Error);
    }
}
