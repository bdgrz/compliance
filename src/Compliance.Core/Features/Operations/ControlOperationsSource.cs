using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Loads the control and program ledger an operations request acts on.</summary>
static class ControlOperationsSource
{
    public static async ValueTask<ControlDraft?> LoadControlAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, Uuid controlId, CancellationToken ct)
    {
        var control = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        return control.IsVisible && control.ProgramId == programId ? control : null;
    }

    /// <summary>
    ///     Each visible planned control's current plan and its recorded and expected occurrences
    ///     starting on or before the horizon.
    /// </summary>
    public static async ValueTask<IReadOnlyList<PlannedControlOccurrences>> ReadPlannedAsync(
        IAggregateReader reader, ControlOperationsLedger ledger, Uuid tenantId, Uuid programId,
        DateOnly today, DateOnly horizon, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var planned = new List<PlannedControlOccurrences>();
        foreach (var controlId in ledger.PlannedControlIds.ToArray())
        {
            if (ledger.CurrentPlan(controlId) is not { } plan)
                continue;
            var control = await LoadControlAsync(reader, tenantId, programId, controlId, ct)
                .ConfigureAwait(false);
            if (control is null)
                continue;
            planned.Add(new PlannedControlOccurrences(controlId, plan,
                control.ApprovedVersion?.Identifier ?? controlId.ToString(),
                ledger.ReadOccurrences(controlId, OperatingAuthority.VersionWindows(control),
                    today, horizon)));
        }
        return planned;
    }

    public static RequestError ControlNotFound() =>
        new(RequestErrorKind.NotFound, "The control was not found.");

    public static RequestError OccurrenceNotFound() =>
        new(RequestErrorKind.NotFound, "The control occurrence was not found.");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>
    ///     Resolves the performer of record. The owner or backup owner, directly or through a team,
    ///     performs as themselves; a workforce person holder's off-product performance may be
    ///     recorded by such a holder or a program manager, attributing both.
    /// </summary>
    public static async ValueTask<Result<OperatingHolder>> ResolvePerformerAsync(
        OperatingAuthority authority, Uuid tenantId, Uuid programId,
        ControlOperatingPlanView plan, OperationsActor actor, Uuid? personId, CancellationToken ct)
    {
        var holds = await authority.HoldsAsync(tenantId, plan.Owner, actor.MemberId, ct)
                        .ConfigureAwait(false) ||
                    await authority.HoldsAsync(tenantId, plan.BackupOwner, actor.MemberId, ct)
                        .ConfigureAwait(false);
        if (personId is { } person)
        {
            var personHolder = new OperatingHolder(OperatingAuthority.PersonHolder, person);
            if (plan.Owner != personHolder && plan.BackupOwner != personHolder)
                return Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The performer person must hold the control's owner or backup owner responsibility."));
            return holds || await authority.ManagesProgramAsync(tenantId, actor, programId, ct)
                .ConfigureAwait(false)
                ? Result<OperatingHolder>.Success(personHolder)
                : Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only an operating holder or a program manager may record a person's performance."));
        }
        return holds
            ? Result<OperatingHolder>.Success(new OperatingHolder(OperatingAuthority.MemberHolder,
                actor.MemberId))
            : Result<OperatingHolder>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the control owner or backup owner may perform and attest to this occurrence."));
    }
}
