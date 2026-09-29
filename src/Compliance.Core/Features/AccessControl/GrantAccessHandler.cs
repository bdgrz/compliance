using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GrantAccessHandler(IAggregateExecutor executor,
    IAccessGrantProposalValidator validator) : IRequestHandler<GrantAccess>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GrantAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        var validation = await validator.ValidateAsync(request.TenantId, request.Proposal, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;

        var proposal = request.Proposal;
        var terms = new AccessGrantTerms(proposal.Principal, proposal.RoleId, proposal.Scope,
            proposal.Source, AccessGrantActor.From(context, request.TenantId),
            proposal.EffectiveFrom, proposal.EffectiveUntil);
        return await executor.ExecuteAsync(new AccessGrant(request.TenantId, request.GrantId),
            grant => AggregateOutcome.CommitOnSuccess(grant.Issue(terms)), context, ct)
            .ConfigureAwait(false);
    }
}
