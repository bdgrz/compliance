using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.user-identity.recovery-options", 1)]
public sealed record GetIdentityRecoveryOptions(
    string EmailAddress,
    Uuid ChallengeId,
    string Token,
    int? Limit = null,
    string? Cursor = null,
    long? MinimumProjectionRevision = null) : IRequest<IdentityRecoveryOptions>, ICallable;

public sealed record IdentityRecoveryOptions(
    long ProjectionRevision,
    Page<IdentityRecoveryOption> Identities);

public sealed record IdentityRecoveryOption(Uuid UserIdentityId, string Provider);
