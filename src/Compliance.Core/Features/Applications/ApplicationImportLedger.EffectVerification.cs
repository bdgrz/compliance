using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    readonly Dictionary<Uuid, ulong> _planSealVersions = [];

    /// <summary>Verifies durable target records; this alone does not authorize the visibility commit.</summary>
    public Result<IReadOnlyList<ApplicationImportEffectPending>> VerifyPendingEffects(ImportBatch batch,
        long expectedRevision, IReadOnlyDictionary<Uuid, DeclaredApplication> targets)
        => VerifyPendingEffects(batch, expectedRevision, targets, null);

    /// <summary>Verifies durable target records and current retirement impact against the frozen plan.</summary>
    public Result<IReadOnlyList<ApplicationImportEffectPending>> VerifyPendingEffects(ImportBatch batch,
        long expectedRevision, IReadOnlyDictionary<Uuid, DeclaredApplication> targets,
        IReadOnlyDictionary<Uuid, ApplicationChangePreview>? retirementImpacts)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(targets);
        if (!BelongsToSource(batch))
            return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found for this source."));
        if (GetState(batch) != "accepting" || _acceptingBatchId != batch.Id ||
            !_frozenPlans.TryGetValue(batch.Id, out var plan))
            return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import batch has no active frozen plan."));
        if (GetRevision(batch) != expectedRevision)
            return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(
                VersionedRecordRules.StaleRevision("import", GetRevision(batch)).ToRequestError());
        if (batch.CommittedStreamPosition == 0 || CommittedStreamPosition < _planSealVersions[batch.Id])
            return EffectsNotDurable();
        var retirements = plan.Rows.Where(row => row.Decision == "retire").ToArray();
        if ((retirements.Length == 0 && retirementImpacts is { Count: > 0 }) ||
            (retirements.Length > 0 && (retirementImpacts is null || retirementImpacts.Count != retirements.Length ||
                retirements.Any(row => !retirementImpacts.TryGetValue(row.ApplicationId, out var impact) ||
                    !ApplicationImportRetirementImpact.MatchesFrozen(impact, _tenantId, row)))))
            return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "Current retirement impact does not match the frozen source plan.",
                isTransient: true));
        var effects = new List<ApplicationImportEffectPending>(plan.Rows.Count);
        foreach (var row in plan.Rows)
        {
            if (!targets.TryGetValue(row.ApplicationId, out var target))
                return EffectsNotDurable();
            if (target.Stream.Realm != _tenantId.ToString() || target.Id != row.ApplicationId)
                return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The import target was not found in this tenant."));
            var effect = target.GetPendingImportEffect(batch.Id, row.RowId);
            if (effect is null || target.CommittedStreamPosition < effect.Metadata.AggregateVersion)
                return EffectsNotDurable();
            if (effect != new ApplicationImportEffectPending(_tenantId, row.ApplicationId,
                    plan.Revision, plan.PlanSha256, plan.Start, row, effect.RecordedAt))
                return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The durable import effect differs from its frozen plan."));
            if ((row.Decision != "retire" && target.IsRetired) ||
                (row.Decision == "create_new" && (target.IsCreated || target.Revision != 0)) ||
                (row.Decision == "link_existing" && (!target.IsCreated || target.Revision != row.ExpectedApplicationRevision)) ||
                (row.Decision == "retire" && (!target.IsCreated || target.IsRetired ||
                    target.Revision != row.ExpectedApplicationRevision)))
                return Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The governed import target changed after its frozen plan."));
            effects.Add(effect);
        }
        return Result<IReadOnlyList<ApplicationImportEffectPending>>.Success(effects.AsReadOnly());
    }

    static Result<IReadOnlyList<ApplicationImportEffectPending>> EffectsNotDurable() =>
        Result<IReadOnlyList<ApplicationImportEffectPending>>.Failure(new RequestError(
            RequestErrorKind.Conflict, "The complete import plan and every target effect must be durable before commit.",
            isTransient: true));
}
