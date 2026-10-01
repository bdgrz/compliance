using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Shows what a proposed plan would schedule, conflict with, and reassign; records nothing.</summary>
public sealed class PreviewControlOperatingPlanHandler(IAggregateReader reader,
    OperatingAuthority authority)
    : IRequestHandler<PreviewControlOperatingPlan, ControlOperatingPlanPreview>
{
    const int UpcomingCount = 4;

    public async ValueTask<Result<ControlOperatingPlanPreview>> HandleAsync(
        IRequestContext<PreviewControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        var checkedPlan = await OperatingPlanChecks.CheckAsync(reader, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.ControlVersionId, request.Owner,
            request.BackupOwner, request.ReviewerMemberId, ct).ConfigureAwait(false);
        if (!checkedPlan.IsSuccess)
            return Result<ControlOperatingPlanPreview>.Failure(checkedPlan.Error!);
        var (version, reviewerHoldsWork) = checkedPlan.Value;
        var ledger = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (ControlOperationsLedger.ValidatePlan(version, request.Owner, request.BackupOwner,
                request.ReviewerMemberId, request.Cadence, request.EffectiveFrom,
                ledger.CurrentPlan(request.ControlId)) is { } invalid)
            return Result<ControlOperatingPlanPreview>.Failure(new RequestError(
                RequestErrorKind.Validation, invalid.Message!));
        var upcoming = ControlCadenceSchedule.Periods(request.Cadence, request.EffectiveFrom,
                version.EffectiveUntil, request.EffectiveFrom.AddYears(2))
            .Take(UpcomingCount)
            .Select(period => new ControlOccurrenceView(request.TenantId, request.ProgramId,
                request.ControlId, ControlCadenceSchedule.OccurrenceId(request.ControlId,
                    period.Start), 0, ControlOperationsLedger.Expected,
                ControlOperationsLedger.Expected, version.VersionId, Uuid.Empty, period.Start,
                period.End, period.DueOn, null, request.Owner, [], [], []))
            .ToArray();
        string[] conflicts = reviewerHoldsWork
            ? ["self_review: the reviewer also holds the control's operating work and needs an approved SoD waiver."]
            : [];
        return Result<ControlOperatingPlanPreview>.Success(new ControlOperatingPlanPreview(
            ControlCadenceSchedule.Describe(request.Cadence),
            version.Content.ExpectedEvidenceDescriptions, upcoming, conflicts,
            ledger.OpenWorkToReassign(request.ControlId, request.Owner, Uuid.Empty)));
    }
}
