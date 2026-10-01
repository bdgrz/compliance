using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Runs one command against the program's evidence request ledger and returns the request.</summary>
static class EvidenceRequestCommands
{
    public static ValueTask<Result<EvidenceRequestView>> ExecuteAsync<TRequest>(IAggregateExecutor executor,
        IRequestContext<TRequest> context, Uuid tenantId, Uuid programId, Uuid requestId,
        Func<EvidenceRequestLedger, CommandFailure?> command, CancellationToken ct)
        where TRequest : IRequestBase =>
        executor.ExecuteAsync(new EvidenceRequestLedger(tenantId, programId), ledger =>
        {
            var failure = command(ledger);
            return CommandFailureRequestAdapter.ToOutcome(failure, ledger.Find(requestId)!);
        }, context, ct);
}
