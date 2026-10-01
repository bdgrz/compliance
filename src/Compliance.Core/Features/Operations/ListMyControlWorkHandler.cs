using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The acting member's operating responsibilities, held directly or through a team, and their
///     ordered work: due, missed, open, or returned occurrences they own; occurrences awaiting
///     their review; and their open corrective actions. Items order by due date, then
///     materiality, then creation, then source ID (M0-D15).
/// </summary>
public sealed class ListMyControlWorkHandler(IAggregateReader reader,
    OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<ListMyControlWork, MyControlWorkView>
{
    const int DefaultHorizonDays = 30;

    public async ValueTask<Result<MyControlWorkView>> HandleAsync(
        IRequestContext<ListMyControlWork> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.HorizonDays is < 0 or > ControlCadenceSchedule.MaximumDueWithinDays)
            return Result<MyControlWorkView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The horizon must be between 0 and 365 days."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var today = ControlOperationsSource.Today(clock);
        var horizon = today.AddDays(request.HorizonDays ?? DefaultHorizonDays);
        var ledger = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var responsibilities = new List<ControlResponsibilityView>();
        var items = new List<(WorkItemView Item, DateTimeOffset CreatedAt)>();
        foreach (var (controlId, plan, identifier, occurrences) in await ControlOperationsSource
                     .ReadPlannedAsync(reader, ledger, request.TenantId, request.ProgramId, today,
                         horizon, ct).ConfigureAwait(false))
        {
            var owns = await authority.HoldsAsync(request.TenantId, plan.Owner, actor.MemberId, ct)
                .ConfigureAwait(false);
            var backs = await authority.HoldsAsync(request.TenantId, plan.BackupOwner,
                actor.MemberId, ct).ConfigureAwait(false);
            var reviews = plan.ReviewerMemberId == actor.MemberId;
            void Add(string role, OperatingHolder holder) => responsibilities.Add(new(controlId,
                plan.PlanVersionId, role, holder, plan.EffectiveFrom, plan.EffectiveUntil,
                plan.CadenceDescription));
            if (owns)
                Add("owner", plan.Owner);
            if (backs)
                Add("backup_owner", plan.BackupOwner!);
            if (reviews)
                Add("reviewer", new OperatingHolder(OperatingAuthority.MemberHolder, actor.MemberId));
            if (!owns && !backs && !reviews)
                continue;
            foreach (var occurrence in occurrences)
            {
                var opened = occurrence.Reviews.Count > 0 || occurrence.Attestations.Count > 0
                    ? occurrence.Attestations[^1].RecordedAt
                    : DateTimeOffset.MinValue;
                if ((owns || backs) && occurrence.State is ControlOperationsLedger.Expected or
                        ControlOperationsLedger.Missed or ControlOperationsLedger.Open or
                        ControlOperationsLedger.Returned)
                    items.Add((Item("control_occurrence", occurrence.OccurrenceId, controlId, null,
                        $"Perform {identifier} for {occurrence.PeriodStart:yyyy-MM-dd}",
                        occurrence.DueOn, today, null), opened));
                if (reviews && occurrence.State is ControlOperationsLedger.Submitted or
                        ControlOperationsLedger.Deferred)
                    items.Add((Item("occurrence_review", occurrence.OccurrenceId, controlId, null,
                        $"Review {identifier} for {occurrence.PeriodStart:yyyy-MM-dd}",
                        occurrence.DueOn, today, null), opened));
            }
        }
        var remediation = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        foreach (var finding in remediation.ReadAll(clock.GetUtcNow())
                     .Where(static finding => finding.Status != RemediationLedger.Closed))
            foreach (var action in finding.CorrectiveActions.Where(action =>
                         action.OwnerMemberId == actor.MemberId &&
                         action.Status == RemediationLedger.Open))
                items.Add((Item("corrective_action", action.ActionId, null, finding.FindingId,
                    action.Description, action.DueOn, today,
                    RemediationLedger.Materiality(finding.Severity)), action.AddedAt));
        var ordered = items
            .OrderBy(static entry => entry.Item.DueOn ?? DateOnly.MaxValue)
            .ThenBy(static entry => MaterialityRank(entry.Item.Materiality))
            .ThenBy(static entry => entry.CreatedAt)
            .ThenBy(static entry => entry.Item.SourceId.ToString(), StringComparer.Ordinal)
            .Select(static entry => entry.Item)
            .ToArray();
        return Result<MyControlWorkView>.Success(new MyControlWorkView(responsibilities, ordered));
    }

    static WorkItemView Item(string kind, Uuid sourceId, Uuid? controlId, Uuid? findingId,
        string summary, DateOnly? dueOn, DateOnly today, string? materiality) =>
        new(kind, sourceId, controlId, findingId, summary, dueOn, dueOn < today, materiality);

    static int MaterialityRank(string? materiality) => materiality switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };
}
