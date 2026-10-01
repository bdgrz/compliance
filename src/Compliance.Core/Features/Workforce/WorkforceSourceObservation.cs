using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Owns an immutable source observation and its attributable reconciliation decision.</summary>
public sealed class WorkforceSourceObservation : Aggregate
{
    readonly Uuid _tenantId;
    WorkforceSourceObserved? _observation;

    public bool IsRecorded => _observation is not null;
    public WorkforceSourceObserved? Observation => _observation;
    public WorkforceSourceDecision? Decision { get; private set; }
    public long Revision { get; private set; }

    public WorkforceSourceObservation(Uuid tenantId, Uuid observationId)
        : base(observationId, new EventStreamAddress(tenantId.ToString(),
            "workforce-source-observations", observationId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkforceSourceObserved>(ev =>
        {
            _observation = ev;
            Revision = 1;
        });
        On<WorkforceSourceReconciled>(ev =>
        {
            Decision = ev.Decision;
            Revision = ev.Revision;
        });
    }

    public static Uuid IdFor(Uuid tenantId, WorkforceSourceIdentity source)
    {
        static string Part(string? value) => $"{value?.Trim().Length ?? 0}:{value?.Trim()}";
        return Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, "workforce-source-observations"),
            Part(source.SourceKind) + Part(source.SourceSystem) + Part(source.SourceRecordId) +
            Part(source.SourceRevision));
    }

    public Result<WorkforceSourceRegistration> Record(WorkforceSourceIdentity source,
        string targetKind, Uuid targetId, long observedTargetRevision, WorkforceSourceFacts facts,
        DateTimeOffset observedAt, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (source is null || facts is null || targetId == Uuid.Empty ||
            observedTargetRevision < 1 || observedAt > recordedAt)
            return Invalid("A source observation requires a target, positive revision, facts, and a non-future observation time.");
        var cleanSource = source with
        {
            SourceKind = source.SourceKind?.Trim() ?? string.Empty,
            SourceSystem = source.SourceSystem?.Trim() ?? string.Empty,
            SourceRecordId = source.SourceRecordId?.Trim() ?? string.Empty,
            SourceRevision = source.SourceRevision?.Trim() ?? string.Empty,
        };
        if (cleanSource.SourceSystem.Length is 0 or > 200 ||
            cleanSource.SourceRecordId.Length is 0 or > 500 ||
            cleanSource.SourceRevision.Length is 0 or > 200)
            return Invalid("A source requires a system (1–200), record ID (1–500), and revision (1–200 characters).");
        var compatible = targetKind switch
        {
            "person" => cleanSource.SourceKind is "hris" or "idp" && facts.Person is not null &&
                        facts.WorkRelationship is null && facts.ServiceIdentity is null,
            "work_relationship" => cleanSource.SourceKind is "hris" or "idp" &&
                                   facts.WorkRelationship is not null && facts.Person is null &&
                                   facts.ServiceIdentity is null,
            "service_identity" => cleanSource.SourceKind == "provider" &&
                                  facts.ServiceIdentity is not null && facts.Person is null &&
                                  facts.WorkRelationship is null,
            _ => false,
        };
        if (!compatible)
            return Invalid("HRIS/IdP observations require person or work_relationship facts; provider observations require service_identity facts.");
        if (Id != IdFor(_tenantId, cleanSource))
            return Invalid("The observation ID must identify its immutable source revision.");
        source = cleanSource;
        facts = WorkforceSourceReconciliation.Normalize(facts);
        if (!WorkforceSourceReconciliation.HasBoundedFacts(facts))
            return Invalid("Observed facts must fit the canonical workforce field limits.");
        var ev = new WorkforceSourceObserved(_tenantId, Id, source, targetKind, targetId,
            observedTargetRevision, facts, observedAt, actor, recordedAt);
        if (_observation is not null)
            return _observation with { Actor = actor, RecordedAt = recordedAt } == ev
                ? Result<WorkforceSourceRegistration>.Success(new WorkforceSourceRegistration(Id))
                : Result<WorkforceSourceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The source revision was already observed with different content or correlation."));
        RaiseEvent(ev);
        return Result<WorkforceSourceRegistration>.Success(new WorkforceSourceRegistration(Id));
    }

    static Result<WorkforceSourceRegistration> Invalid(string message) =>
        Result<WorkforceSourceRegistration>.Failure(new RequestError(RequestErrorKind.Validation, message));

    public CommandFailure? Reconcile(long expectedRevision, long expectedTargetRevision,
        long currentTargetRevision, WorkforceSourceFacts currentFacts, string outcome, string note,
        ActorReference actor, DateTimeOffset decidedAt)
    {
        if (_observation is null)
            return CommandFailure.MissingRecord("The source observation was not found.");
        var cleanNote = note?.Trim() ?? string.Empty;
        if (outcome is not ("accepted" or "dismissed") || cleanNote.Length is 0 or > 1000)
            return CommandFailure.InvalidContent("A decision requires accepted or dismissed and a note of 1–1000 characters.");
        if (Decision is not null)
            return Decision.Outcome == outcome && Decision.Note == cleanNote &&
                   Decision.TargetRevision == expectedTargetRevision
                ? null
                : CommandFailure.StateConflict("The source observation already has a different decision.");
        if (expectedRevision != Revision || expectedTargetRevision < 1 ||
            expectedTargetRevision != currentTargetRevision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("source or canonical workforce record", Revision));
        if (outcome == "accepted" && NormalizeFacts(_observation.Facts) != NormalizeFacts(currentFacts))
            return CommandFailure.StateConflict("Conflicting source facts require an explicit canonical revision or an attributed dismissal before reconciliation.");
        RaiseEvent(new WorkforceSourceReconciled(_tenantId, Id, Revision + 1,
            new WorkforceSourceDecision(outcome, cleanNote, currentTargetRevision, actor, decidedAt)));
        return null;
    }

    static WorkforceSourceFacts NormalizeFacts(WorkforceSourceFacts facts) =>
        WorkforceSourceReconciliation.Normalize(facts);
}
