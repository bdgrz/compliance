using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

sealed class FitzRiskEvaluationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/risk-evaluation-directory-v1/projection",
            ProjectorName),
        IRiskEvaluationDirectoryReader, IRiskEvaluationDirectoryProjection
{
    public const string ProjectorName = "RiskEvaluationDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RiskAssessmentRecorded recorded:
                await ApplyAsync(recorded.TenantId, recorded.ProgramId, recorded.RiskId,
                    recorded.Revision, "assessment_recorded", recorded.Assessment.Assessor,
                    recorded.Assessment.AssessedAt,
                    view => view with { Assessments = [.. view.Assessments, recorded.Assessment] },
                    recorded.Assessment, null, null, ct).ConfigureAwait(false);
                break;
            case RiskTreatmentChosen chosen:
                await ApplyAsync(chosen.TenantId, chosen.ProgramId, chosen.RiskId,
                    chosen.Revision, "treatment_chosen", chosen.Treatment.ChosenBy,
                    chosen.Treatment.ChosenAt, view => view with { Treatment = chosen.Treatment },
                    null, chosen.Treatment, null, ct).ConfigureAwait(false);
                break;
            case RiskAccepted accepted:
                await ApplyAsync(accepted.TenantId, accepted.ProgramId, accepted.RiskId,
                    accepted.Revision, "risk_accepted", accepted.Acceptance.Approver,
                    accepted.Acceptance.AcceptedAt,
                    view => view with { Acceptances = [.. view.Acceptances, accepted.Acceptance] },
                    null, null, accepted.Acceptance, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ApplyAsync(Uuid tenantId, Uuid programId, Uuid riskId, long revision,
        string kind, ActorReference actor, DateTimeOffset at,
        Func<RiskEvaluationView, RiskEvaluationView> change, RiskAssessmentView? assessment,
        RiskTreatmentView? treatment, RiskAcceptanceView? acceptance, CancellationToken ct)
    {
        var current = await RiskEvaluationDirectorySchema.Evaluations.GetAsync(Transaction,
            riskId, ct).ConfigureAwait(false);
        if (current is null ? revision != 1
            : current.TenantId != tenantId || current.ProgramId != programId ||
              current.Revision + 1 != revision)
            throw new InvalidOperationException(
                "A risk evaluation revision cannot project before its predecessor.");
        var next = change(current ?? new RiskEvaluationView(tenantId, programId, riskId, 0,
            "unassessed", [], null, [], null, null, null)) with
        {
            Revision = revision,
            LastChangedBy = actor,
            LastChangedAt = at,
        };
        if (current is null)
            await RiskEvaluationDirectorySchema.Evaluations.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await RiskEvaluationDirectorySchema.Evaluations.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
        await RiskEvaluationDirectorySchema.History.InsertAsync(Transaction,
            new RiskEvaluationHistoryEntryView(tenantId, programId, riskId, revision, kind,
                assessment, treatment, acceptance, actor, at), ct).ConfigureAwait(false);
    }

    public async ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RiskEvaluationDirectorySchema.Evaluations.GetAsync(tx, riskId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId,
        Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RiskEvaluationDirectorySchema.History.QueryAsync(tx,
            RiskEvaluationDirectorySchema.ByRiskRevision.Query()
                .WithPrefix(riskId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
