using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Owns one tenant-scoped instance identity and its immutable application parent.</summary>
public sealed class DeclaredSystemInstance : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    Uuid _applicationId;
    string? _name;
    string? _kind;
    string? _accessBoundaryReference;
    string? _sourceIdentifier;
    long _revision;

    public bool IsCreated => _created;
    public Uuid ApplicationId => _applicationId;
    public long Revision => _revision;

    public DeclaredSystemInstance(Uuid tenantId, Uuid instanceId)
        : base(instanceId, new EventStreamAddress(tenantId.ToString(), "system-instances",
            instanceId.ToString()))
    {
        _tenantId = tenantId;
        On<SystemInstanceRegistered>(ev =>
        {
            _created = true;
            _applicationId = ev.ApplicationId;
            _name = ev.Name;
            _kind = ev.Kind;
            _accessBoundaryReference = ev.AccessBoundaryReference;
            _sourceIdentifier = ev.SourceIdentifier;
            _revision = ev.Revision;
        });
    }

    public Result<SystemInstanceRegistration> Declare(Uuid applicationId, string name, string kind,
        string? accessBoundaryReference, string? sourceIdentifier, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (applicationId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            string.IsNullOrWhiteSpace(kind) || kind.Length > 200 ||
            accessBoundaryReference is { Length: > 2000 } ||
            sourceIdentifier is { Length: > 1000 })
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A system instance requires its own identity, name, kind, and a bounded boundary reference."));

        if (_created)
            return IsSameDeclaration(_applicationId, _name!, _kind!, _accessBoundaryReference,
                    _sourceIdentifier, applicationId, name, kind, accessBoundaryReference,
                    sourceIdentifier)
                ? Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(Id))
                : Result<SystemInstanceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance already exists with different content."));

        RaiseEvent(new SystemInstanceRegistered(_tenantId, applicationId, Id, 1,
            name.Trim(), kind.Trim(), NormalizeOptional(accessBoundaryReference),
            NormalizeOptional(sourceIdentifier),
            actorMemberId, actorDisplay, changedAt));
        return Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(Id));
    }

    /// <summary>Compares a recorded declaration with a requested one after normalization.</summary>
    public static bool IsSameDeclaration(Uuid recordedApplicationId, string recordedName,
        string recordedKind, string? recordedBoundary, string? recordedSource,
        Uuid applicationId, string? name, string? kind, string? accessBoundaryReference,
        string? sourceIdentifier) =>
        recordedApplicationId == applicationId && recordedName == name?.Trim() &&
        recordedKind == kind?.Trim() &&
        recordedBoundary == NormalizeOptional(accessBoundaryReference) &&
        recordedSource == NormalizeOptional(sourceIdentifier);

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
