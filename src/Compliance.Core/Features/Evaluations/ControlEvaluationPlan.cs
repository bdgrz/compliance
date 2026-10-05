using System.Collections.ObjectModel;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Stores immutable, versioned evaluation procedures for one control.</summary>
public sealed class ControlEvaluationPlan : Aggregate
{
    const int MaximumObjectiveLength = 4000;
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<ControlEvaluationPlanVersionView> _versions = [];

    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public long Revision => _versions.LastOrDefault()?.Version ?? 0;
    public ControlEvaluationPlanVersionView? CurrentVersion => _versions.LastOrDefault();

    public ControlEvaluationPlan(Uuid tenantId, Uuid programId, Uuid controlId)
        : base(controlId, new EventStreamAddress(tenantId.ToString(),
            "control-evaluation-plans", controlId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ControlEvaluationPlanVersionDefined>(ev =>
        {
            _versions.Add(new ControlEvaluationPlanVersionView(ev.TenantId, ev.ProgramId,
                ev.ControlId, ev.ControlVersionId, ev.PlanVersionId, ev.Version, ev.Objective,
                Normalize(ev.Steps),
                ev.TesterIndependenceRequired, ev.AuthoredByMemberId, ev.Author, ev.DefinedAt));
        });
    }

    public IReadOnlyList<ControlEvaluationPlanVersionView> ReadVersions() => _versions.ToArray();

    public ControlEvaluationPlanVersionView? FindVersion(Uuid planVersionId) =>
        _versions.FirstOrDefault(version => version.PlanVersionId == planVersionId);

    public CommandFailure? Define(long expectedVersion, Uuid controlVersionId,
        Uuid planVersionId, string objective,
        IReadOnlyList<EvaluationProcedureStep>? steps, bool testerIndependenceRequired,
        Uuid authoredByMemberId, ActorReference author, DateTimeOffset definedAt)
    {
        ArgumentNullException.ThrowIfNull(author);
        if (controlVersionId == Uuid.Empty || planVersionId == Uuid.Empty ||
            authoredByMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(objective) || objective.Trim().Length > MaximumObjectiveLength)
            return CommandFailure.InvalidContent(
                "An evaluation plan version requires an objective and an attributed author.");
        if (ControlEvaluationLedger.ValidateProcedure(steps) is { } invalid)
            return invalid;
        if (steps!.SelectMany(static step => step.InspectedItems).Any(item =>
                item.Kind == "control" && (item.Reference != Id.ToString() ||
                    item.Version != controlVersionId.ToString())))
            return CommandFailure.InvalidContent(
                "A control scope reference must match the plan control and exact control version.");

        var existing = FindVersion(planVersionId);
        if (existing is not null)
            return existing.ControlVersionId == controlVersionId &&
                   existing.Objective == objective.Trim() &&
                   existing.TesterIndependenceRequired == testerIndependenceRequired &&
                   existing.AuthoredByMemberId == authoredByMemberId &&
                   SameSteps(existing.Steps, Normalize(steps))
                ? null
                : CommandFailure.StateConflict(
                    "The evaluation plan version ID was already used for different content.");

        if (expectedVersion != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control evaluation plan", Revision));

        RaiseEvent(new ControlEvaluationPlanVersionDefined(_tenantId, _programId, Id,
            controlVersionId, planVersionId, Revision + 1, objective.Trim(), Normalize(steps),
            testerIndependenceRequired, authoredByMemberId, author, definedAt));
        return null;
    }

    static bool SameSteps(IReadOnlyList<EvaluationProcedureStep> left,
        ReadOnlyCollection<EvaluationProcedureStep> right) => left.Count == right.Count &&
        left.Zip(right).All(static pair => pair.First.Assertion == pair.Second.Assertion &&
            pair.First.Method == pair.Second.Method &&
            pair.First.ExpectedCondition == pair.Second.ExpectedCondition &&
            pair.First.InspectedItems.SequenceEqual(pair.Second.InspectedItems));

    static ReadOnlyCollection<EvaluationProcedureStep> Normalize(
        IReadOnlyList<EvaluationProcedureStep>? steps) => Array.AsReadOnly((steps ?? []).Select(
            static step => new EvaluationProcedureStep(step.Assertion, step.Method,
                Array.AsReadOnly(step.InspectedItems.Select(static item =>
                    new EvaluationInspectedItem(item.Kind.Trim(), item.Reference.Trim(),
                        item.Version.Trim())).ToArray()), step.ExpectedCondition.Trim())).ToArray());
}
