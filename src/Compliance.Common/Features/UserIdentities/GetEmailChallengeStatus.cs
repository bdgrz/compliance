using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed record GetEmailChallengeStatus(Uuid UserId, string EmailAddress)
    : IRequest<EmailChallengeStatusView>, IEmailOwnershipRequest, ICallable;

public sealed record EmailChallengeStatusView(string DeliveryStatus, DateTimeOffset? ExpiresAt);
