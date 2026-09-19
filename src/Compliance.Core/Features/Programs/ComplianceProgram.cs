using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ComplianceProgram : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _initialName;
    ProgramPlan? _initialPlan;

    public bool IsCreated => _created;

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
        On<ProgramRevised>(ev => _revision = ev.Revision);
    }

    public Result<ProgramRegistration> Create(string name, ProgramPlan plan, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (_created)
            return StringComparer.Ordinal.Equals(_initialName, name?.Trim()) &&
                   Equals(_initialPlan, plan)
                ? Result<ProgramRegistration>.Success(new ProgramRegistration(Id))
                : Result<ProgramRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The program already exists with different content."));
        var error = Validate(name, plan);
        if (error is not null)
            return Result<ProgramRegistration>.Failure(error);
        RaiseEvent(new ProgramCreated(_tenantId, Id, name.Trim(), plan,
            actorMemberId, actorDisplay, changedAt));
        return Result<ProgramRegistration>.Success(new ProgramRegistration(Id));
    }

    public Result Revise(long expectedRevision, string name, ProgramPlan plan,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The program changed. Reload it before revising."));
        var error = Validate(name, plan);
        if (error is not null)
            return Result.Failure(error);
        RaiseEvent(new ProgramRevised(_tenantId, Id, _revision + 1, name.Trim(), plan,
            actorMemberId, actorDisplay, changedAt));
        return Result.Success;
    }

    static RequestError? Validate(string name, ProgramPlan? plan)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new RequestError(RequestErrorKind.Validation, "A program requires a name.");
        if (plan is null)
            return new RequestError(RequestErrorKind.Validation, "A program requires a plan.");
        if (plan.TargetReadinessDate > plan.TargetTypeIAsOfDate)
            return new RequestError(RequestErrorKind.Validation,
                "The Type I target date must follow the readiness target date.");
        if (plan.TargetTypeIIStartDate > plan.TargetTypeIIEndDate)
            return new RequestError(RequestErrorKind.Validation,
                "The Type II target end date must follow its start date.");
        if (plan.TargetTypeIAsOfDate > plan.TargetTypeIIStartDate)
            return new RequestError(RequestErrorKind.Validation,
                "The Type II target period must follow the Type I target date.");
        return null;
    }
}
