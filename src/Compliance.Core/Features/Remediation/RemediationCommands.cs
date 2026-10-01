using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Runs one finding command against the program ledger and returns the finding view.</summary>
static class RemediationCommands
{
    public static async ValueTask<Result<FindingView>> ExecuteAsync<TRequest>(
        IAggregateExecutor executor, IRequestContext<TRequest> context, Uuid tenantId,
        Uuid programId, Uuid findingId, DateTimeOffset now,
        Func<RemediationLedger, CommandFailure?> command, CancellationToken ct)
        where TRequest : IRequestBase =>
        await executor.ExecuteAsync(new RemediationLedger(tenantId, programId), ledger =>
        {
            var failure = command(ledger);
            return CommandFailureRequestAdapter.ToOutcome(failure, ledger.Read(findingId, now)!);
        }, context, ct).ConfigureAwait(false);

    public static async ValueTask<bool> IsActiveMemberAsync(IAggregateReader reader,
        Uuid tenantId, Uuid memberId, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && member.Affiliation != "firm_staff";
    }

    public static RequestError InactiveOwner() => new(RequestErrorKind.Validation,
        "The owner must be an active client member.");
}
