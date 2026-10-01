using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Resolves the occurrence, its plan, and the performer of record before an attestation.</summary>
static class OccurrenceAttestation
{
    public static async ValueTask<Result<(OperationsActor Actor, OperatingHolder Performer,
        IReadOnlyDictionary<Uuid, DateOnly?> Windows)>> PrepareAsync(IAggregateReader reader,
        OperatingAuthority authority, Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid occurrenceId, Uuid? personId, ClaimsPrincipal principal, DateTimeOffset now,
        CancellationToken ct)
    {
        var control = await ControlOperationsSource.LoadControlAsync(reader, tenantId, programId,
            controlId, ct).ConfigureAwait(false);
        if (control is null)
            return Failure(ControlOperationsSource.ControlNotFound());
        var windows = OperatingAuthority.VersionWindows(control);
        var ledger = await reader.HydrateAsync(new ControlOperationsLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (ledger.ReadOccurrence(controlId, occurrenceId, windows,
                DateOnly.FromDateTime(now.UtcDateTime)) is not { } occurrence ||
            ledger.FindPlan(controlId, occurrence.PlanVersionId) is not { } plan)
            return Failure(ControlOperationsSource.OccurrenceNotFound());
        // Ownership is evaluated against the plan now in force, so reassignment moves who may act.
        var governing = ledger.CurrentPlan(controlId) ?? plan;
        var actor = OperationsActor.From(principal, tenantId);
        var performer = await ControlOperationsSource.ResolvePerformerAsync(authority, tenantId,
            programId, governing, actor, personId, ct).ConfigureAwait(false);
        return performer.IsSuccess
            ? Result<(OperationsActor, OperatingHolder, IReadOnlyDictionary<Uuid, DateOnly?>)>
                .Success((actor, performer.Value, windows))
            : Failure(performer.Error!);
    }

    static Result<(OperationsActor, OperatingHolder, IReadOnlyDictionary<Uuid, DateOnly?>)>
        Failure(RequestError error) =>
        Result<(OperationsActor, OperatingHolder, IReadOnlyDictionary<Uuid, DateOnly?>)>
            .Failure(error);
}
