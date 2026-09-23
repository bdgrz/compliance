using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Delivery boundary for a verification challenge. The domain stores only a token hash.</summary>
public interface IEmailChallengeDelivery
{
    ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct);
}
