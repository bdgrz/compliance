using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IAccessGrantProposalValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
        CancellationToken ct = default);
}
