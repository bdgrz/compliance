using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class AccessGrantProposalValidator(ITenantMembershipDirectoryReader memberships,
    ITeamDirectoryReader teams, IRoleDirectoryReader roles,
    IProgramResourceScopeResolver resourceScopes)
    : IAccessGrantProposalValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || proposal is null || proposal.Principal is null ||
            proposal.Scope is null || proposal.Source is null || !Enum.IsDefined(proposal.Scope.Kind))
            return Invalid("The access grant proposal is incomplete.");

        var role = await roles.GetAsync(tenantId, proposal.RoleId, ct).ConfigureAwait(false);
        if (role is null || role.RoleId != proposal.RoleId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The access grant role was not found in this organization."));

        var principal = await ValidatePrincipalAsync(tenantId, proposal.Principal, ct).ConfigureAwait(false);
        if (!principal.IsSuccess)
            return principal;

        if (proposal.Scope.Kind == AccessGrantScopeKind.Organization && proposal.Scope.Id != tenantId)
            return Invalid("The organization grant scope must match the request organization.");

        if (proposal.Scope.Kind == AccessGrantScopeKind.Program)
        {
            if (!await resourceScopes.IsTenantProgramAsync(tenantId, proposal.Scope.Id, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind != AccessGrantScopeKind.Organization)
            return Invalid("This access grant scope is not supported.");

        return Result.Success;
    }

    async ValueTask<Result> ValidatePrincipalAsync(Uuid tenantId, AccessGrantPrincipal principal,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(principal.Kind) || principal.Id == Uuid.Empty)
            return Invalid("The access grant principal is invalid.");

        if (principal.Kind == AccessGrantPrincipalKind.Team)
        {
            var team = await teams.GetAsync(tenantId, principal.Id, ct).ConfigureAwait(false);
            return team is not null && team.TeamId == principal.Id
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The access grant team was not found in this organization."));
        }

        string? cursor = null;
        do
        {
            var page = await memberships.ListAsync(tenantId, 100, cursor, ct).ConfigureAwait(false);
            foreach (var membership in page.Items)
            {
                if (membership.TenantId != tenantId ||
                    RbacIds.Member(tenantId, membership.UserId) != principal.Id)
                    continue;
                return membership.Affiliation == "client_personnel"
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                        "Firm staff require an accepted engagement assignment for client access."));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result.Failure(new RequestError(RequestErrorKind.NotFound,
            "The access grant member was not found in this organization."));
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));
}
