using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.access-grant.issue", 1)]
public sealed record GrantAccess(Uuid TenantId, Uuid GrantId, AccessGrantProposal Proposal)
    : IRequest, IRbacManagementRequest, ICallable;
