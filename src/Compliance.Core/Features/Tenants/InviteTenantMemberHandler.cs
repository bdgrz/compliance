using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteTenantMemberHandler(IAggregateReader reader, IAggregateExecutor executor,
    ITenantInvitationDelivery delivery, TimeProvider clock) : IRequestHandler<InviteTenantMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<InviteTenantMember> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        if (!tenant.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant does not exist."));

        var invitedBy = RequestActor.IsSystem(context.Actor)
            ? tenant.OperatorUserId
            : UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
                ? userId
                : Uuid.Empty;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = clock.GetUtcNow();
        var result = await executor.ExecuteAsync(
            new TenantInvitation(context.Request.TenantId, normalized),
            invitation => AggregateOutcome.CommitOnSuccess(invitation.Invite(
                context.Request.Affiliation, context.Request.Administrator, hash, now.AddDays(7), now, invitedBy)),
            context, ct).ConfigureAwait(false);
        if (result.IsSuccess)
            await delivery.SendAsync(context.Request.TenantId, normalized, token, ct).ConfigureAwait(false);
        return result;
    }
}
