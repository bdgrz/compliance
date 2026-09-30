using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Owns one governed non-human identity (M0-D06): exactly one accountable owner, a person or a
///     team, an approved purpose, and a review date at most one year out. Groups and roles are
///     never owners.
/// </summary>
public sealed class ServiceIdentity : Aggregate
{
    static readonly string[] IdentityKinds = ["workload", "service", "automation", "bot", "integration"];
    static readonly string[] LifecycleStatuses = ["active", "disabled", "retired"];
    static readonly string[] OwnerKinds = ["person", "team"];

    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    ServiceIdentityTerms? _initialTerms;

    public bool IsCreated => _created;
    public long Revision => _revision;

    public ServiceIdentity(Uuid tenantId, Uuid serviceIdentityId)
        : base(serviceIdentityId, new EventStreamAddress(tenantId.ToString(), "service-identities",
            serviceIdentityId.ToString()))
    {
        _tenantId = tenantId;
        On<ServiceIdentityRecorded>(ev =>
        {
            _created = true;
            _revision = 1;
            _initialTerms = ev.Terms;
        });
        On<ServiceIdentityRevised>(ev => _revision = ev.Revision);
    }

    public Result<ServiceIdentityRegistration> Record(ServiceIdentityTerms terms, DateOnly today,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var validation = Validate(terms, today);
        if (validation is not null)
            return Result<ServiceIdentityRegistration>.Failure(validation);
        var clean = Clean(terms);
        if (_created)
            return _initialTerms == clean
                ? Result<ServiceIdentityRegistration>.Success(new ServiceIdentityRegistration(Id))
                : Result<ServiceIdentityRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The service identity already exists with different content."));
        RaiseEvent(new ServiceIdentityRecorded(_tenantId, Id, clean, actor, changedAt));
        return Result<ServiceIdentityRegistration>.Success(new ServiceIdentityRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, ServiceIdentityTerms terms, DateOnly today,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The service identity was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("service identity", _revision));
        var validation = Validate(terms, today);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation.Message!);
        RaiseEvent(new ServiceIdentityRevised(_tenantId, Id, _revision + 1, Clean(terms), actor,
            changedAt));
        return null;
    }

    static RequestError? Validate(ServiceIdentityTerms terms, DateOnly today)
    {
        static RequestError Invalid(string message) => new(RequestErrorKind.Validation, message);
        if (terms is null || string.IsNullOrWhiteSpace(terms.DisplayName) ||
            terms.DisplayName.Trim().Length > 200)
            return Invalid("A service identity requires a display name of at most 200 characters.");
        if (!IdentityKinds.Contains(terms.IdentityKind))
            return Invalid(
                "The identity kind must be workload, service, automation, bot, or integration.");
        if (string.IsNullOrWhiteSpace(terms.Purpose) || terms.Purpose.Trim().Length > 1000)
            return Invalid("A service identity requires an approved purpose of at most 1000 characters.");
        if (terms.Environment is { } environment && environment.Trim().Length > 100)
            return Invalid("The environment must be at most 100 characters.");
        if (!LifecycleStatuses.Contains(terms.LifecycleStatus))
            return Invalid("The lifecycle status must be active, disabled, or retired.");
        if (!OwnerKinds.Contains(terms.OwnerKind) || terms.OwnerId == Uuid.Empty)
            return Invalid("A service identity requires exactly one accountable person or team owner.");
        if (terms.ReviewBy > today.AddYears(1))
            return Invalid("The review date must be at most one year out.");
        if (terms.LifecycleStatus != "retired" && terms.ReviewBy <= today)
            return Invalid("The review date must be in the future.");
        if (terms.LifecycleStatus == "active" && terms.ExpiresOn is { } expiresOn &&
            expiresOn <= today)
            return Invalid("An active service identity cannot already be expired.");
        return null;
    }

    static ServiceIdentityTerms Clean(ServiceIdentityTerms terms) => terms with
    {
        DisplayName = terms.DisplayName.Trim(),
        Purpose = terms.Purpose.Trim(),
        Environment = string.IsNullOrWhiteSpace(terms.Environment) ? null : terms.Environment.Trim(),
    };
}
