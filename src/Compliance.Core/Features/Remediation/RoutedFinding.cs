using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Raises a reactor-routed finding with the product defaults (medium severity, due in 30
///     days) and an optional corrective action. Replays are idempotent by finding ID.
/// </summary>
static class RoutedFinding
{
    public const int DefaultDueDays = 30;
    public const string DefaultSeverity = "medium";

    public static async ValueTask<Result> RaiseAsync<TRequest>(IAggregateExecutor executor,
        IRequestContext<TRequest> context, Uuid tenantId, Uuid programId, Uuid findingId,
        FindingSource source, string title, string affectedScope, Uuid ownerMemberId,
        IReadOnlyList<FindingLink> links, string? correctiveAction, ActorReference actor,
        DateTimeOffset now, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(DefaultDueDays);
        return await executor.ExecuteAsync(new RemediationLedger(tenantId, programId), ledger =>
        {
            if (ledger.Contains(findingId))
                return CommandFailureRequestAdapter.ToOutcome(null);
            var failure = ledger.Raise(findingId, source, title, source.SourceText,
                DefaultSeverity, affectedScope, ownerMemberId, dueOn, links, actor, now);
            if (failure is null && correctiveAction is not null)
                failure = ledger.AddAction(findingId, 1,
                    Uuid.CreateVersion5(findingId, "corrective-action"), correctiveAction,
                    ownerMemberId, dueOn, actor, now);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
    }
}
