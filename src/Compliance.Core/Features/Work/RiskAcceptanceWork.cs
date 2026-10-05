using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives pending R1-07 risk acceptance work from each risk's source records.</summary>
static class RiskAcceptanceWork
{
    public const string Kind = "risk_acceptance";

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, IRiskDraftDirectoryReader directory,
        RiskDraftListReadConsistency? consistency, Uuid tenantId, Uuid programId,
        DateTimeOffset now, Uuid? workItemId, CancellationToken ct)
    {
        var fence = ProjectionCheckpoint.Start;
        if (consistency is not null)
        {
            var captured = await consistency.CaptureFenceAsync(tenantId, ct).ConfigureAwait(false);
            if (!captured.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(captured.Error);
            fence = captured.Value;
        }

        var candidates = new List<WorkCandidate>();
        var methods = await reader.HydrateAsync(new RiskMethod(tenantId, programId), ct)
            .ConfigureAwait(false);
        var governance = await reader.HydrateAsync(new RiskGovernanceLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items.Any(risk => risk.TenantId != tenantId || risk.ProgramId != programId))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            foreach (var draft in page.Items)
            {
                var evaluation = await reader.HydrateAsync(new RiskEvaluation(tenantId,
                    draft.RiskId), ct).ConfigureAwait(false);
                var source = evaluation.ToView();
                source = source with
                {
                    OpenReassessmentTriggers = governance.OpenTriggers(draft.RiskId,
                        source.Assessments),
                };
                var view = RiskEvaluationStatus.AsOf(source, now);
                if (view.Status != "acceptance_pending" ||
                    evaluation.Treatment is not { Kind: "accept" } treatment ||
                    view.Assessments.LastOrDefault(static assessment =>
                        assessment.Phase == RiskEvaluation.Residual) is not { } residual)
                    continue;
                if (residual.AssessedAt < treatment.ChosenAt)
                    continue;

                var method = methods.Find(residual.MethodVersionId);
                if (method is null)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());
                var executiveRequired = method.AppetiteThreshold is not { } appetite ||
                                        residual.Score > appetite;
                var excluded = new HashSet<Uuid>();
                if (residual.Assessor.Kind == "member")
                    excluded.Add(Uuid.Parse(residual.Assessor.Id, CultureInfo.InvariantCulture));
                if (governance.OwnerOf(draft.RiskId) is { } owner)
                {
                    if (owner.CorrelatedMemberId is { } recordedOwner)
                        excluded.Add(recordedOwner);
                    var (_, currentOwner) = await RiskOwnerResolution.ResolveAsync(reader,
                        tenantId, owner.PersonId, ct).ConfigureAwait(false);
                    if (currentOwner is { } memberId)
                        excluded.Add(memberId);
                }

                var identity = Uuid.CreateVersion5(draft.RiskId,
                    $"risk-acceptance\n{residual.AssessmentId}");
                var itemId = WorkCandidate.IdFor(identity, Kind);
                if (workItemId is { } wanted && itemId != wanted)
                    continue;
                var requiredRole = executiveRequired ? "executive" : "compliance lead or executive";
                candidates.Add(new WorkCandidate(itemId, Kind, draft.RiskId, null, null,
                    $"Approve risk acceptance {draft.Identifier}",
                    $"The current residual assessment {residual.AssessmentId} is awaiting " +
                    $"time-bounded approval by a {requiredRole}.", null, null, "accept",
                    $"/api/v1/tenants/{tenantId}/programs/{programId}/risks/{draft.RiskId}/acceptances",
                    new OperatingHolder(executiveRequired
                            ? OperatingAuthority.RiskExecutiveHolder
                            : OperatingAuthority.RiskApproverHolder,
                        programId), null, excluded, residual.AssessedAt));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (consistency is not null)
        {
            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence, ct)
                .ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(confirmed.Error);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    public static bool HasRestrictedAuthority(WorkCandidate candidate) =>
        candidate.Responsible.Kind is OperatingAuthority.RiskApproverHolder or
            OperatingAuthority.RiskExecutiveHolder;

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The risk work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The risk evaluation and method sources differ. Retry the query.", isTransient: true);
}
