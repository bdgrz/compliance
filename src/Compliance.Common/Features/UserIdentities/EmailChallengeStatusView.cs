namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed record EmailChallengeStatusView(string DeliveryStatus, DateTimeOffset? ExpiresAt);
