using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class AccessGrantProposalValidator(ITenantMembershipDirectoryReader memberships,
    ITeamDirectoryReader teams, IRoleDirectoryReader roles,
    IProgramResourceScopeResolver resourceScopes, IAggregateReader reader,
    IApplicationDirectoryReader? applications = null, IDomainEventReader? events = null)
    : IAccessGrantProposalValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || proposal is null || proposal.Principal is null ||
            proposal.Scope is null || proposal.Source is null || !Enum.IsDefined(proposal.Scope.Kind))
            return Invalid("The access grant proposal is incomplete.");
        if (proposal.Scope.ResourceType is not null &&
            proposal.Scope.Kind != AccessGrantScopeKind.SharedResource)
            return Invalid("Only a shared-resource scope may specify a resource type.");
        if (proposal.Scope.Kind == AccessGrantScopeKind.SharedResource &&
            proposal.Scope.ResourceType is not (TechnologyInventoryResourceTypes.InformationAsset or
                TechnologyInventoryResourceTypes.DataFlow))
            return Invalid("This shared-resource access grant type is not supported.");

        var role = await roles.GetAsync(tenantId, proposal.RoleId, ct).ConfigureAwait(false);
        if (role is null || role.RoleId != proposal.RoleId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The access grant role was not found in this organization."));
        var sourceRole = await reader.HydrateAsync(new Role(tenantId, proposal.RoleId), ct)
            .ConfigureAwait(false);
        if (!sourceRole.IsActive)
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
        else if (proposal.Scope.Kind == AccessGrantScopeKind.Application)
        {
            var application = await reader.HydrateAsync(new DeclaredApplication(tenantId,
                proposal.Scope.Id), ct).ConfigureAwait(false);
            if (!application.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind == AccessGrantScopeKind.SystemInstance)
        {
            var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
                proposal.Scope.Id), ct).ConfigureAwait(false);
            if (!instance.IsCreated &&
                !await IsLegacySystemInstanceAsync(tenantId, proposal.Scope.Id, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The SystemInstance scope was not found in this organization."));
        }
        else if (proposal.Scope.Kind == AccessGrantScopeKind.SharedResource)
        {
            var exists = proposal.Scope.ResourceType switch
            {
                TechnologyInventoryResourceTypes.InformationAsset =>
                    (await reader.HydrateAsync(new InformationAsset(tenantId, proposal.Scope.Id), ct)
                        .ConfigureAwait(false)).IsCreated,
                TechnologyInventoryResourceTypes.DataFlow =>
                    (await reader.HydrateAsync(new DataFlow(tenantId, proposal.Scope.Id), ct)
                        .ConfigureAwait(false)).IsCreated,
                _ => false,
            };
            if (!exists)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The shared technology inventory resource was not found in this organization."));
        }
        else if (proposal.Scope.Kind != AccessGrantScopeKind.Organization)
            return Invalid("This access grant scope is not supported.");

        return Result.Success;
    }

    async ValueTask<bool> IsLegacySystemInstanceAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct)
    {
        if (applications is null || events is null)
            return false;
        var projected = await applications.GetInstanceAsync(tenantId, instanceId, ct)
            .ConfigureAwait(false);
        if (projected is { } instance)
        {
            if (instance.TenantId != tenantId || instance.SystemInstanceId != instanceId ||
                instance.LegacyApplicationRevision is null ||
                !(await reader.HydrateAsync(new DeclaredApplication(tenantId,
                    instance.ApplicationId), ct).ConfigureAwait(false)).IsCreated)
                return false;
            return await ScopedSystemInstanceSource.FindAsync(reader, events, tenantId,
                instance.ApplicationId, instanceId, ct).ConfigureAwait(false) is not null;
        }

        var pending = await new LegacySystemInstanceSource(applications, events)
            .FindPendingAsync(tenantId, instanceId, ct).ConfigureAwait(false);
        if (pending.Match is not SystemInstanceDeclared declaration)
            return false;
        return (await reader.HydrateAsync(new DeclaredApplication(tenantId,
            declaration.ApplicationId), ct).ConfigureAwait(false)).IsCreated;
    }

    async ValueTask<Result> ValidatePrincipalAsync(Uuid tenantId, AccessGrantPrincipal principal,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(principal.Kind) || principal.Id == Uuid.Empty)
            return Invalid("The access grant principal is invalid.");

        if (principal.Kind == AccessGrantPrincipalKind.Team)
        {
            var team = await teams.GetAsync(tenantId, principal.Id, ct).ConfigureAwait(false);
            var sourceTeam = team is not null && team.TeamId == principal.Id
                ? await reader.HydrateAsync(new Team(tenantId, principal.Id), ct)
                    .ConfigureAwait(false)
                : null;
            return sourceTeam is { IsActive: true }
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
                if (membership.IsDeprovisioned)
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The access grant member was not found in this organization."));
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
