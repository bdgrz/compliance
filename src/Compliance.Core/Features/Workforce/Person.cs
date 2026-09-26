using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Owns one manually recorded workforce person and its revision history.</summary>
public sealed class Person : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _initialDisplayName;
    string? _initialWorkEmail;

    public bool IsCreated => _created;
    public long Revision => _revision;

    public Person(Uuid tenantId, Uuid personId)
        : base(personId, new EventStreamAddress(tenantId.ToString(), "people",
            personId.ToString()))
    {
        _tenantId = tenantId;
        On<PersonRecorded>(ev =>
        {
            _created = true;
            _revision = 1;
            _initialDisplayName = ev.DisplayName;
            _initialWorkEmail = ev.WorkEmail;
        });
        On<PersonRevised>(ev => _revision = ev.Revision);
    }

    public Result<PersonRegistration> Record(string displayName, string? workEmail,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var validation = Validate(displayName, workEmail);
        if (validation is not null)
            return Result<PersonRegistration>.Failure(validation);
        var name = displayName.Trim();
        var email = NormalizeOptional(workEmail);
        if (_created)
            return _initialDisplayName == name && _initialWorkEmail == email
                ? Result<PersonRegistration>.Success(new PersonRegistration(Id))
                : Result<PersonRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The person already exists with different content."));
        RaiseEvent(new PersonRecorded(_tenantId, Id, name, email, actor, changedAt));
        return Result<PersonRegistration>.Success(new PersonRegistration(Id));
    }

    public Result Revise(long expectedRevision, string displayName, string? workEmail,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The person was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("person", _revision)
                .ToRequestError());
        var validation = Validate(displayName, workEmail);
        if (validation is not null)
            return Result.Failure(validation);
        RaiseEvent(new PersonRevised(_tenantId, Id, _revision + 1, displayName.Trim(),
            NormalizeOptional(workEmail), actor, changedAt));
        return Result.Success;
    }

    static RequestError? Validate(string displayName, string? workEmail)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            return new RequestError(RequestErrorKind.Validation,
                "A person requires a display name of at most 200 characters.");
        var email = NormalizeOptional(workEmail);
        if (email is not null && (email.Length > 320 || email.IndexOf('@') is < 1 ||
                                  email.IndexOf('@') == email.Length - 1 ||
                                  email.Any(char.IsWhiteSpace)))
            return new RequestError(RequestErrorKind.Validation,
                "A work email must be a single address of at most 320 characters.");
        return null;
    }

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
