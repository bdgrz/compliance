using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records a new attestation version superseding the latest; HTTP-only.</summary>
public sealed class CorrectControlAttestationHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<CorrectControlAttestation, ControlOccurrenceView>
{
    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<CorrectControlAttestation> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var prepared = await OccurrenceAttestation.PrepareAsync(reader, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.OccurrenceId, request.PerformedByPersonId,
            context.Actor, now, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ControlOccurrenceView>.Failure(prepared.Error!);
        var (actor, performer, windows) = prepared.Value;
        var input = new AttestationInput(request.Result, request.PerformedAt, request.CoveredFrom,
            request.CoveredUntil, request.Notes, request.Rationale, request.Evidence ?? [],
            performer);
        return await executor.ExecuteAsync(new ControlOperationsLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Correct(request.ControlId, request.OccurrenceId,
                    request.ExpectedRevision, input, request.CorrectionReason, actor.MemberId,
                    actor.Display, now);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.ReadOccurrence(request.ControlId, request.OccurrenceId, windows,
                        DateOnly.FromDateTime(now.UtcDateTime))!);
            }, context, ct).ConfigureAwait(false);
    }
}
