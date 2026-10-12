using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Owns one tenant-declared application and its metadata history.</summary>
public sealed partial class DeclaredApplication : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _initialName;
    string? _initialPurpose;
    string? _initialOwnerReference;
    string? _initialClassification;
    string? _initialApplicationKind;
    string? _initialCriticality;
    Uuid? _initialSystemOwner;
    Uuid? _initialAccessOwner;
    bool _initialIsRestricted;

    bool _retired;
    bool _isRestricted;
    string? _classification;
    string? _applicationKind;
    string? _criticality;
    Uuid? _systemOwnerPersonId;

    public bool IsCreated => _created;
    public bool IsRetired => _retired;
    public long Revision => _revision;
    public Uuid? AccessOwnerPersonId { get; private set; }
    public bool IsRestricted => _isRestricted;

    public DeclaredApplication(Uuid tenantId, Uuid applicationId)
        : base(applicationId, new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString()))
    {
        _tenantId = tenantId;
        RegisterImportEffectEvents();
        On<ApplicationDeclared>(ev =>
        {
            _created = true;
            _revision = 1;
            _initialName = ev.Name;
            _initialPurpose = ev.Purpose;
            _initialOwnerReference = ev.OwnerReference;
            _initialClassification = ev.Classification;
            _initialApplicationKind = ev.ApplicationKind;
            _initialCriticality = ev.Criticality;
            _initialSystemOwner = ev.SystemOwnerPersonId;
            _initialAccessOwner = ev.AccessOwnerPersonId;
            _initialIsRestricted = ev.IsRestricted;
            _isRestricted = ev.IsRestricted;
            _classification = ev.Classification;
            _applicationKind = ev.ApplicationKind;
            _criticality = ev.Criticality;
            _systemOwnerPersonId = ev.SystemOwnerPersonId;
            AccessOwnerPersonId = ev.AccessOwnerPersonId;
        });
        On<ApplicationRevised>(ev =>
        {
            _revision = ev.Revision;
            _classification = ev.Classification;
            _systemOwnerPersonId = ev.SystemOwnerPersonId;
            _applicationKind = ev.ApplicationKind ?? _applicationKind;
            _criticality = ev.Criticality ?? _criticality;
            AccessOwnerPersonId = ev.AccessOwnerPersonId;
            _isRestricted = ev.IsRestricted;
        });
        On<ApplicationRetired>(ev =>
        {
            _revision = ev.Revision;
            _retired = true;
        });
        // Historical application-stream declarations still own their old revision numbers.
        // Their instance state is projected separately and is not retained by this aggregate.
        On<SystemInstanceDeclared>(ev => _revision = ev.ApplicationRevision);
    }

    public Result<ApplicationRegistration> Declare(string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null,
        Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool isRestricted = false, string? applicationKind = null,
        string? criticality = null, bool governedDeclarationsEnabled = false)
    {
        var normalizedOwner = NormalizeOptional(ownerReference);
        var normalizedClassification = NormalizeOptional(classification);
        var normalizedKind = NormalizeOptional(applicationKind);
        var normalizedCriticality = NormalizeOptional(criticality);
        if (!_created && _pendingImportEffects.Values.Any(ev => ev.Row.Decision == "create_new"))
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application identity is reserved by an import plan."));
        if (_created)
            return _initialName == name?.Trim() && _initialPurpose == purpose?.Trim() &&
                   _initialOwnerReference == normalizedOwner &&
                   _initialClassification == normalizedClassification &&
                   _initialApplicationKind == normalizedKind &&
                   _initialCriticality == normalizedCriticality &&
                   _initialSystemOwner == systemOwnerPersonId &&
                   _initialAccessOwner == accessOwnerPersonId &&
                   _initialIsRestricted == isRestricted
                ? Result<ApplicationRegistration>.Success(new ApplicationRegistration(Id))
                : Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The application already exists with different content."));
        var validation = Validate(name, purpose, ownerReference, classification);
        if (validation is not null)
            return Result<ApplicationRegistration>.Failure(validation);
        if (applicationKind is { Length: > 200 } || criticality is { Length: > 200 })
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "Application kind and criticality cannot exceed 200 characters each."));
        if (governedDeclarationsEnabled && (normalizedClassification is null ||
            systemOwnerPersonId is null || systemOwnerPersonId == Uuid.Empty ||
            accessOwnerPersonId is null || accessOwnerPersonId == Uuid.Empty ||
            normalizedKind is null || normalizedCriticality is null))
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "An application requires classification, system and access owners, application kind and criticality."));
        RaiseEvent(new ApplicationDeclared(_tenantId, Id, name.Trim(), purpose.Trim(),
            normalizedOwner, actorMemberId, actorDisplay, changedAt, normalizedClassification,
            systemOwnerPersonId, accessOwnerPersonId, isRestricted, normalizedKind, normalizedCriticality)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<ApplicationRegistration>.Success(new ApplicationRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null,
        Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool? isRestricted = null, string? applicationKind = null,
        string? criticality = null, bool governedRevisionsEnabled = false)
    {
        var check = ResolveRevisionContent(expectedRevision, name, purpose, ownerReference,
            classification, systemOwnerPersonId, accessOwnerPersonId, isRestricted,
            applicationKind, criticality, governedRevisionsEnabled, out var content);
        if (check is not null)
            return check;
        RaiseEvent(new ApplicationRevised(_tenantId, Id, _revision + 1, content.Name,
            content.Purpose, content.OwnerReference, actorMemberId,
            actorDisplay, changedAt, content.Classification, content.SystemOwnerPersonId,
            content.AccessOwnerPersonId, content.IsRestricted, content.ApplicationKind, content.Criticality)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    internal CommandFailure? ResolveRevisionContent(long expectedRevision, string name,
        string purpose, string? ownerReference, string? classification,
        Uuid? systemOwnerPersonId, Uuid? accessOwnerPersonId, bool? isRestricted,
        string? applicationKind, string? criticality, bool governed,
        out ApplicationRevisionContent content)
    {
        content = new ApplicationRevisionContent(name?.Trim()!, purpose?.Trim()!,
            NormalizeOptional(ownerReference),
            classification?.Trim() ?? (governed ? _classification?.Trim() : null),
            systemOwnerPersonId ?? (governed ? _systemOwnerPersonId : null),
            accessOwnerPersonId ?? (governed ? AccessOwnerPersonId : null),
            isRestricted ?? _isRestricted, applicationKind?.Trim() ?? _applicationKind?.Trim(),
            criticality?.Trim() ?? _criticality?.Trim());
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return check;
        var validation = Validate(name!, purpose!, ownerReference, classification);
        return validation is not null
            ? CommandFailure.InvalidContent(validation.Message!)
            : content.Validate(governed);
    }

    /// <summary>Retires the application from an effective date without deleting its history.</summary>
    public CommandFailure? Retire(long expectedRevision, DateTimeOffset effectiveAt,
        string reason, Uuid? mergedIntoApplicationId, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return check;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return CommandFailure.InvalidContent(
                "A retirement requires a reason of at most 2000 characters.");
        if (mergedIntoApplicationId == Id || mergedIntoApplicationId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "An application cannot be merged into itself or an empty identity.");
        RaiseEvent(new ApplicationRetired(_tenantId, Id, _revision + 1, effectiveAt,
            reason.Trim(), mergedIntoApplicationId, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    CommandFailure? CheckChange(long expectedRevision) => !_created
        ? CommandFailure.MissingRecord("The application was not found.")
        : _retired ? CommandFailure.StateConflict("The application is retired.")
        : CheckPendingImportChanges() is { } importError ? CommandFailure.StateConflict(importError.Message!)
        : expectedRevision == _revision ? null :
            CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("application", _revision));

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
