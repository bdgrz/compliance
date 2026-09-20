using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class TenantInvitationIssuer(IAggregateExecutor executor,
    ITenantInvitationDelivery delivery, TimeProvider clock)
{
    public async ValueTask<Result> IssueAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, string affiliation, bool administrator,
        string? builtInRole, Uuid invitedBy, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if (!EmailAddresses.TryNormalize(emailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = clock.GetUtcNow();
        var result = await executor.ExecuteAsync(new TenantInvitation(tenantId, normalized),
            invitation => AggregateOutcome.CommitOnSuccess(invitation.Invite(affiliation,
                administrator, hash, now.AddDays(7), now, invitedBy, builtInRole)),
            context, ct).ConfigureAwait(false);
        if (result.IsSuccess)
            await delivery.SendAsync(tenantId, normalized, token, ct).ConfigureAwait(false);
        return result;
    }
}
