using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ComplianceProgram : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    Uuid? _criteriaEditionId;
    bool _lastChangeWasSelection;
    string? _initialName;
    ProgramPlan? _initialPlan;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? CriteriaEditionId => _criteriaEditionId;

    public ComplianceProgram(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "programs", programId.ToString()))
    {
        _tenantId = tenantId;
        On<ProgramCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            _initialName = ev.Name;
            _initialPlan = ev.Plan;
        });
        On<ProgramRevised>(ev =>
        {
            _revision = ev.Revision;
            _lastChangeWasSelection = false;
        });
        On<ProgramCriteriaEditionSelected>(ev =>
        {
            _revision = ev.Revision;
            _criteriaEditionId = ev.EditionId;
            _lastChangeWasSelection = true;
        });
    }

    public CommandFailure? Create(string name, ProgramPlan plan, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (_created)
            return StringComparer.Ordinal.Equals(_initialName, name?.Trim()) &&
                   Equals(_initialPlan, plan)
                ? null
                : CommandFailure.StateConflict(
                    "The program already exists with different content.");
        var error = Validate(name, plan);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        RaiseEvent(new ProgramCreated(_tenantId, Id, name.Trim(), plan,
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? Revise(long expectedRevision, string name, ProgramPlan plan,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The program was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("program", _revision));
        var error = Validate(name, plan);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        RaiseEvent(new ProgramRevised(_tenantId, Id, _revision + 1, name.Trim(), plan,
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? SelectCriteriaEdition(long expectedRevision, Uuid editionId,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The program was not found.");
        if (editionId == Uuid.Empty)
            return CommandFailure.InvalidContent("A criteria edition is required.");
        // An exact retry either finds the edition already current at the expected revision,
        // or finds that its own selection is the only change since that revision.
        if (_criteriaEditionId == editionId &&
            (expectedRevision == _revision ||
             (_lastChangeWasSelection && expectedRevision + 1 == _revision)))
            return null;
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("program", _revision));
        RaiseEvent(new ProgramCriteriaEditionSelected(_tenantId, Id, _revision + 1,
            editionId, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    static string? Validate(string name, ProgramPlan? plan)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "A program requires a name.";
        if (plan is null)
            return "A program requires a plan.";
        if (plan.TargetReadinessDate > plan.TargetTypeIAsOfDate)
            return "The Type I target date must follow the readiness target date.";
        if (plan.TargetTypeIIStartDate > plan.TargetTypeIIEndDate)
            return "The Type II target end date must follow its start date.";
        if (plan.TargetTypeIAsOfDate > plan.TargetTypeIIStartDate)
            return "The Type II target period must follow the Type I target date.";
        return null;
    }
}
