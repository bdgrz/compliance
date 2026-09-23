using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Owns one tenant-declared application and its concrete instance identities.</summary>
public sealed class DeclaredApplication : Aggregate
{
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, (string Name, string Kind, string? AccessBoundaryReference,
        string? SourceIdentifier)> _instances = [];
    bool _created;
    long _revision;
    string? _initialName;
    string? _initialPurpose;
    string? _initialOwnerReference;
    string? _initialClassification;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public bool HasInstance(Uuid instanceId) => _created && _instances.ContainsKey(instanceId);

    public DeclaredApplication(Uuid tenantId, Uuid applicationId)
        : base(applicationId, new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString()))
    {
        _tenantId = tenantId;
        On<ApplicationDeclared>(ev =>
        {
            _created = true;
            _revision = 1;
            _initialName = ev.Name;
            _initialPurpose = ev.Purpose;
            _initialOwnerReference = ev.OwnerReference;
            _initialClassification = ev.Classification;
        });
        On<ApplicationRevised>(ev => _revision = ev.Revision);
        On<SystemInstanceDeclared>(ev =>
        {
            _revision = ev.ApplicationRevision;
            _instances.Add(ev.SystemInstanceId, (ev.Name, ev.Kind, ev.AccessBoundaryReference,
                ev.SourceIdentifier));
        });
    }

    public Result<ApplicationRegistration> Declare(string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null)
    {
        var normalizedOwner = NormalizeOptional(ownerReference);
        var normalizedClassification = NormalizeOptional(classification);
        if (_created)
            return _initialName == name?.Trim() && _initialPurpose == purpose?.Trim() &&
                   _initialOwnerReference == normalizedOwner &&
                   _initialClassification == normalizedClassification
                ? Result<ApplicationRegistration>.Success(new ApplicationRegistration(Id))
                : Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The application already exists with different content."));
        var validation = Validate(name, purpose, ownerReference, classification);
        if (validation is not null)
            return Result<ApplicationRegistration>.Failure(validation);
        RaiseEvent(new ApplicationDeclared(_tenantId, Id, name.Trim(), purpose.Trim(),
            normalizedOwner, actorMemberId, actorDisplay, changedAt, normalizedClassification));
        return Result<ApplicationRegistration>.Success(new ApplicationRegistration(Id));
    }

    public Result Revise(long expectedRevision, string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null)
    {
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return Result.Failure(check);
        var validation = Validate(name, purpose, ownerReference, classification);
        if (validation is not null)
            return Result.Failure(validation);
        RaiseEvent(new ApplicationRevised(_tenantId, Id, _revision + 1, name.Trim(),
            purpose.Trim(), NormalizeOptional(ownerReference), actorMemberId,
            actorDisplay, changedAt, NormalizeOptional(classification)));
        return Result.Success;
    }

    public Result<SystemInstanceRegistration> DeclareInstance(long expectedRevision,
        Uuid instanceId, string name, string kind, string? accessBoundaryReference,
        string? sourceIdentifier,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (instanceId == Uuid.Empty || string.IsNullOrWhiteSpace(name) ||
            name.Length > 200 || string.IsNullOrWhiteSpace(kind) || kind.Length > 200 ||
            accessBoundaryReference is { Length: > 2000 } ||
            sourceIdentifier is { Length: > 1000 })
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A system instance requires its own identity, name, kind, and a bounded boundary reference."));
        if (_instances.TryGetValue(instanceId, out var prior))
            return prior == (name.Trim(), kind.Trim(), NormalizeOptional(accessBoundaryReference),
                       NormalizeOptional(sourceIdentifier))
                ? Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(instanceId))
                : Result<SystemInstanceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance already exists with different content."));
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return Result<SystemInstanceRegistration>.Failure(check);
        RaiseEvent(new SystemInstanceDeclared(_tenantId, Id, instanceId, _revision + 1,
            name.Trim(), kind.Trim(), NormalizeOptional(accessBoundaryReference),
            NormalizeOptional(sourceIdentifier),
            actorMemberId, actorDisplay, changedAt));
        return Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(instanceId));
    }

    RequestError? CheckChange(long expectedRevision) => !_created
        ? new RequestError(RequestErrorKind.NotFound, "The application was not found.")
        : expectedRevision == _revision ? null :
            VersionedRecordRules.StaleRevision("application", _revision).ToRequestError();

    static RequestError? Validate(string name, string purpose, string? ownerReference,
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return new RequestError(RequestErrorKind.Validation,
                "An application requires a name of at most 200 characters.");
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 2000)
            return new RequestError(RequestErrorKind.Validation,
                "An application requires a purpose of at most 2000 characters.");
        if (ownerReference is { Length: > 2000 })
            return new RequestError(RequestErrorKind.Validation,
                "An owner reference cannot exceed 2000 characters.");
        if (classification is { Length: > 200 })
            return new RequestError(RequestErrorKind.Validation,
                "A classification cannot exceed 200 characters.");
        return null;
    }

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
