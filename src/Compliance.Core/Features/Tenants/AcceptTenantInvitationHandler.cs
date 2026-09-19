using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class AcceptTenantInvitationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AcceptTenantInvitation>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AcceptTenantInvitation> context, CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("AcceptTenantInvitationAuthorizer must reject this actor.");
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address.")));
        return executor.ExecuteAsync(new TenantInvitation(context.Request.TenantId, normalized),
            invitation => AggregateOutcome.CommitOnSuccess(
                invitation.Accept(userId, context.Request.Token, clock.GetUtcNow())), context, ct);
    }
}
