using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class CompleteIdentityRecoveryHandler(
    IAggregateReader reader,
    IAggregateWriter writer,
    TimeProvider clock) : IRequestHandler<CompleteIdentityRecovery>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<CompleteIdentityRecovery> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));
        if (!OidcProviderClaims.TryGet(context.Actor, out var provider,
                out var identifier, out var assertedEmail))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Completing identity recovery requires proof of the replacement provider identity."));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CompleteAsync(context, normalized, provider, identifier,
                    assertedEmail, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < 9)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10 << (attempt - 1), 250)), ct)
                    .ConfigureAwait(false);
            }
        }
    }

    async ValueTask<Result> CompleteAsync(IRequestContext<CompleteIdentityRecovery> context,
        string emailAddress, string provider, string identifier, string? assertedEmail,
        CancellationToken ct)
    {
        var address = await reader.HydrateAsync(new EmailAddress(emailAddress), ct).ConfigureAwait(false);
        if (!address.IsVerified || address.Owner is not { } userId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No verified recovery address is available."));

        var replacement = await reader.HydrateAsync(new UserIdentity(provider, identifier), ct)
            .ConfigureAwait(false);
        if (replacement.IsRegistered &&
            (replacement.UserId != userId || replacement.IsRevoked))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The replacement identity is already registered or revoked."));
        if (context.Request.OldIdentityId == replacement.Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A different old identity is required."));

        var retired = await reader.HydrateAsync(
            new UserIdentity(context.Request.OldIdentityId), ct).ConfigureAwait(false);
        if (!retired.IsRegistered || retired.UserId != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The old identity is not linked to this user."));

        var claim = address.ClaimIdentityRecovery(userId, context.Request.ChallengeId,
            context.Request.Token, retired.Id, replacement.Id, clock.GetUtcNow());
        if (!claim.IsSuccess)
            return claim;
        await writer.SaveAsync(address, new RequestDispatchContext(context.Actor), ct)
            .ConfigureAwait(false);

        if (!replacement.IsRegistered)
        {
            var registered = replacement.Register(userId, assertedEmail);
            if (!registered.IsSuccess)
                return Result.Failure(registered.Error);
            await writer.SaveAsync(replacement, new RequestDispatchContext(context.Actor), ct)
                .ConfigureAwait(false);
        }

        if (!retired.IsRevoked)
        {
            var revoked = retired.Revoke(replacement.Id, clock.GetUtcNow());
            if (!revoked.IsSuccess)
                return revoked;
            await writer.SaveAsync(retired, new RequestDispatchContext(context.Actor), ct)
                .ConfigureAwait(false);
        }
        else
        {
            var replayedRevocation = retired.Revoke(replacement.Id, clock.GetUtcNow());
            if (!replayedRevocation.IsSuccess)
                return replayedRevocation;
        }

        var completion = address.CompleteIdentityRecovery(userId, context.Request.ChallengeId,
            retired.Id, replacement.Id, clock.GetUtcNow());
        if (!completion.IsSuccess)
            return completion;
        await writer.SaveAsync(address, new RequestDispatchContext(context.Actor), ct)
            .ConfigureAwait(false);
        return Result.Success;
    }
}
