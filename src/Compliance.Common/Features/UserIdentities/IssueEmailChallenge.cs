using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.issue-challenge", 1)]
public sealed record IssueEmailChallenge(Uuid UserId, string EmailAddress) : IRequest, IEmailOwnershipRequest, ICallable;
