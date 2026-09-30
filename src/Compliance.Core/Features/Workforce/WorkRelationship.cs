using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Owns one person's work relationship, keyed by the tenant and the stable source worker ID so
///     each worker ID identifies exactly one relationship (M0-D06). Email is never an identifier.
/// </summary>
public sealed class WorkRelationship : Aggregate
{
    static readonly string[] WorkerTypes = ["employee", "contractor", "external_collaborator"];
    static readonly string[] LifecycleStatuses = ["pending", "active", "on_leave", "ended"];

    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    Uuid _personId;
    string? _sourceWorkerId;
    WorkRelationshipTerms? _initialTerms;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid PersonId => _personId;

    public WorkRelationship(Uuid tenantId, Uuid relationshipId)
        : base(relationshipId, new EventStreamAddress(tenantId.ToString(), "work-relationships",
            relationshipId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkRelationshipRecorded>(ev =>
        {
            _created = true;
            _revision = 1;
            _personId = ev.PersonId;
            _sourceWorkerId = ev.SourceWorkerId;
            _initialTerms = ev.Terms;
        });
        On<WorkRelationshipRevised>(ev => _revision = ev.Revision);
    }

    public static string NormalizeWorkerId(string? sourceWorkerId) =>
        sourceWorkerId?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, string sourceWorkerId) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, "work-relationships"),
            NormalizeWorkerId(sourceWorkerId));

    public Result<WorkRelationshipRegistration> Record(Uuid personId, string sourceWorkerId,
        WorkRelationshipTerms terms, ActorReference actor, DateTimeOffset changedAt)
    {
        var workerId = NormalizeWorkerId(sourceWorkerId);
        if (workerId.Length is 0 or > 100)
            return Result<WorkRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A source worker ID of 1 to 100 characters is required."));
        var validation = Validate(personId, terms);
        if (validation is not null)
            return Result<WorkRelationshipRegistration>.Failure(validation);
        var clean = Clean(terms);
        if (_created)
            return _personId == personId && _sourceWorkerId == workerId && _initialTerms == clean
                ? Result<WorkRelationshipRegistration>.Success(new WorkRelationshipRegistration(Id))
                : Result<WorkRelationshipRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The source worker ID already identifies a different work relationship."));
        RaiseEvent(new WorkRelationshipRecorded(_tenantId, Id, personId, workerId, clean, actor,
            changedAt));
        return Result<WorkRelationshipRegistration>.Success(new WorkRelationshipRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, WorkRelationshipTerms terms,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The work relationship was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("work relationship", _revision));
        var validation = Validate(_personId, terms);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation.Message!);
        RaiseEvent(new WorkRelationshipRevised(_tenantId, Id, _revision + 1, Clean(terms), actor,
            changedAt));
        return null;
    }

    static RequestError? Validate(Uuid personId, WorkRelationshipTerms terms)
    {
        static RequestError Invalid(string message) => new(RequestErrorKind.Validation, message);
        if (personId == Uuid.Empty)
            return Invalid("A work relationship requires a person.");
        if (terms is null || !WorkerTypes.Contains(terms.WorkerType))
            return Invalid("The worker type must be employee, contractor, or external_collaborator.");
        if (!LifecycleStatuses.Contains(terms.LifecycleStatus))
            return Invalid("The lifecycle status must be pending, active, on_leave, or ended.");
        if (terms.LifecycleStatus == "ended" && terms.EndDate is null)
            return Invalid("An ended work relationship requires an end date.");
        if (terms.EndDate is { } end && end < terms.StartDate)
            return Invalid("The end date cannot be before the start date.");
        if (terms.Department is { } department && department.Trim().Length > 200)
            return Invalid("The department must be at most 200 characters.");
        if (terms.ManagerPersonId == personId || terms.SponsorPersonId == personId)
            return Invalid("A person cannot be their own manager or sponsor.");
        if (terms.WorkerType == "external_collaborator" && terms.SponsorPersonId is null)
            return Invalid("An external collaborator requires an accountable internal sponsor.");
        return null;
    }

    static WorkRelationshipTerms Clean(WorkRelationshipTerms terms) => terms with
    {
        Department = string.IsNullOrWhiteSpace(terms.Department) ? null : terms.Department.Trim(),
    };
}
