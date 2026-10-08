using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Adds the permanent Attest write wall; existing membership and resource authorizers still apply.</summary>
sealed class ClientManagementMutationAuthorizer(ClientManagementIndependenceGuard independence,
    ITenantMembershipDirectoryReader memberships)
    : IRequestAuthorizer<IClientManagementMutationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IClientManagementMutationRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Client management authoring requires a Bdgrz user identity."));
        var membership = await memberships.GetAsync(context.Request.TenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != context.Request.TenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        return await independence.CanAuthorAsync(context.Request.TenantId, userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A person with actual Attest assignment history cannot author or approve this client's management records."));
    }
}
