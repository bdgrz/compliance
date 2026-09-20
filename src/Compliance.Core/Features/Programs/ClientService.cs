using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ClientService : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _retired;
    long _revision;
    Uuid _programId;
    string? _initialName;
    string? _initialPurpose;
    string? _initialOwnerReference;

    public bool IsActive => _created && !_retired;
    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? ProgramId => _programId == Uuid.Empty ? null : _programId;

    public ClientService(Uuid tenantId, Uuid serviceId)
        : base(serviceId, new EventStreamAddress(tenantId.ToString(), "client-services", serviceId.ToString()))
    {
        _tenantId = tenantId;
        On<ClientServiceCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            _programId = ev.ProgramId;
            _initialName = ev.Name;
            _initialPurpose = ev.Purpose;
            _initialOwnerReference = ev.OwnerReference;
        });
        On<ClientServiceRevised>(ev => _revision = ev.Revision);
        On<ClientServiceRetired>(ev => { _retired = true; _revision = ev.Revision; });
    }

    public Result<ClientServiceRegistration> Create(Uuid programId, string name, string purpose,
        string ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (_created)
            return _programId == programId &&
                   StringComparer.Ordinal.Equals(_initialName, name?.Trim()) &&
                   StringComparer.Ordinal.Equals(_initialPurpose, purpose?.Trim()) &&
                   StringComparer.Ordinal.Equals(_initialOwnerReference, ownerReference?.Trim())
                ? Result<ClientServiceRegistration>.Success(new ClientServiceRegistration(Id))
                : Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The service already exists with different content."));
        if (programId == Uuid.Empty)
            return Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A service requires its owning program."));
        var error = Validate(name, purpose, ownerReference);
        if (error is not null)
            return Result<ClientServiceRegistration>.Failure(error);
        RaiseEvent(new ClientServiceCreated(_tenantId, Id, name.Trim(), purpose.Trim(),
            ownerReference.Trim(), actorMemberId, actorDisplay, changedAt, programId));
        return Result<ClientServiceRegistration>.Success(new ClientServiceRegistration(Id));
    }

    public Result Revise(long expectedRevision, string name, string purpose,
        string ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        var error = CheckChange(expectedRevision);
        if (error is not null)
            return Result.Failure(error);
        error = Validate(name, purpose, ownerReference);
        if (error is not null)
            return Result.Failure(error);
        RaiseEvent(new ClientServiceRevised(_tenantId, Id, _revision + 1, name.Trim(),
            purpose.Trim(), ownerReference.Trim(), actorMemberId, actorDisplay, changedAt));
        return Result.Success;
    }

    public Result Retire(long expectedRevision, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        var error = CheckChange(expectedRevision);
        if (error is not null)
            return Result.Failure(error);
        if (string.IsNullOrWhiteSpace(rationale))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Retiring a service requires a rationale."));
        RaiseEvent(new ClientServiceRetired(_tenantId, Id, _revision + 1, rationale.Trim(),
            actorMemberId, actorDisplay, changedAt));
        return Result.Success;
    }

    RequestError? CheckChange(long expectedRevision)
    {
        if (!_created)
            return new RequestError(RequestErrorKind.NotFound, "The service was not found.");
        if (_retired)
            return new RequestError(RequestErrorKind.Conflict, "The service is retired.");
        return expectedRevision == _revision ? null :
            VersionedRecordRules.StaleRevision("service", _revision);
    }

    static RequestError? Validate(string name, string purpose, string ownerReference)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new RequestError(RequestErrorKind.Validation, "A service requires a name.");
        if (string.IsNullOrWhiteSpace(purpose))
            return new RequestError(RequestErrorKind.Validation, "A service requires a purpose.");
        if (string.IsNullOrWhiteSpace(ownerReference))
            return new RequestError(RequestErrorKind.Validation, "A service requires an owner reference.");
        return null;
    }
}
