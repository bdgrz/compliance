using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Adds the retained Attest wall to human client administration without granting bootstrap authority.</summary>
sealed class ClientRbacMutationAuthorizer(ClientManagementIndependenceGuard independence,
    ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<IClientRbacMutationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IClientRbacMutationRequest> context,
        CancellationToken ct)
    {
        // The ordinary RBAC authorizer still decides whether this trusted system operation is allowed.
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Client administration requires a Bdgrz user identity."));
        var membership = await memberships.GetAsync(context.Request.TenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != context.Request.TenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        return await independence.CanAuthorAsync(context.Request.TenantId, userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A person with actual Attest assignment history cannot author this client's administration records."));
    }
}
