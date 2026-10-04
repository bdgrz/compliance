using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GrantAccessHandler(IAggregateExecutor executor,
    IAccessGrantProposalValidator validator, IMemberAccessEligibility memberAccess)
    : IRequestHandler<GrantAccess>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GrantAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        var proposal = request.Proposal;
        if (proposal is null || proposal.Principal is null)
            return await validator.ValidateAsync(request.TenantId, proposal!, ct).ConfigureAwait(false);

        Uuid? membershipEpisodeId = null;
        if (proposal.Principal.Kind == AccessGrantPrincipalKind.Member)
        {
            membershipEpisodeId = await memberAccess.GetMembershipEpisodeIdAsync(request.TenantId,
                proposal.Principal.Id, ct)
                .ConfigureAwait(false);
            if (membershipEpisodeId is null)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The access grant member was not found in this organization."));
        }
        var validation = await validator.ValidateAsync(request.TenantId, proposal, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;

        if (proposal.Principal.Kind == AccessGrantPrincipalKind.Member)
        {
            var currentEpisodeId = await memberAccess.GetMembershipEpisodeIdAsync(request.TenantId,
                proposal.Principal.Id, ct)
                .ConfigureAwait(false);
            if (currentEpisodeId != membershipEpisodeId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The member's tenant authority changed while the grant was being prepared."));
            if (membershipEpisodeId == Uuid.Empty)
                membershipEpisodeId = null;
        }

        var terms = new AccessGrantTerms(proposal.Principal, proposal.RoleId, proposal.Scope,
            proposal.Source, AccessGrantActor.From(context, request.TenantId),
            proposal.EffectiveFrom, proposal.EffectiveUntil);
        return await executor.ExecuteAsync(new AccessGrant(request.TenantId, request.GrantId),
            grant => AggregateOutcome.CommitOnSuccess(grant.Issue(terms, membershipEpisodeId)), context, ct)
            .ConfigureAwait(false);
    }
}
