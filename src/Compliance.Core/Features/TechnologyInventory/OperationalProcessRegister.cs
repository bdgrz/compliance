using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Owns a tenant's operational process inventory: stable IDs, active-name uniqueness, and attributed
///     complete revisions.
/// </summary>
public sealed class OperationalProcessRegister : Aggregate
{
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, (long Revision, OperationalProcessContent Content)> _records = [];

    public OperationalProcessRegister(Uuid tenantId)
        : base(tenantId, InventoryRegisters.Address(tenantId, InventoryRegisters.Processes))
    {
        _tenantId = tenantId;
        On<OperationalProcessRevisionRecorded>(ev =>
        {
            if (ev.TenantId != _tenantId ||
                ev.Revision != (_records.TryGetValue(ev.OperationalProcessId, out var known) ? known.Revision : 0) + 1)
                throw new InvalidOperationException(
                    "A operational process revision must follow its tenant and previous revision.");
            _records[ev.OperationalProcessId] = (ev.Revision, ev.Content);
        });
    }

    public long RevisionOf(Uuid id) => _records.TryGetValue(id, out var known) ? known.Revision : 0;

    public OperationalProcessContent? Get(Uuid id) =>
        _records.TryGetValue(id, out var known) ? known.Content : null;

    public Result<OperationalProcessRegistration> Record(Uuid id, OperationalProcessContent requested, ActorReference actor,
        DateTimeOffset changedAt)
    {
        var content = InventoryRegisterRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = id == Uuid.Empty ? "A operational process requires a nonempty identity." :
            InventoryRegisterRules.Validate(content);
        if (error is not null)
            return Result<OperationalProcessRegistration>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (_records.TryGetValue(id, out var existing))
            return existing.Revision == 1 && existing.Content == content
                ? Result<OperationalProcessRegistration>.Success(new OperationalProcessRegistration(id))
                : Result<OperationalProcessRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The operational process already exists with different content."));
        if (Duplicate(id, content))
            return Result<OperationalProcessRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An active operational process already uses this governed name."));
        RaiseEvent(new OperationalProcessRevisionRecorded(_tenantId, id, 1, content, actor, changedAt));
        return Result<OperationalProcessRegistration>.Success(new OperationalProcessRegistration(id));
    }

    public CommandFailure? Revise(Uuid id, long expectedRevision,
        Func<OperationalProcessContent, OperationalProcessContent> change, ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_records.TryGetValue(id, out var current))
            return CommandFailure.MissingRecord("The operational process was not found.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("operational process",
                current.Revision));
        var content = InventoryRegisterRules.Normalize(change(current.Content));
        var error = InventoryRegisterRules.Validate(content);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        if (Duplicate(id, content))
            return CommandFailure.StateConflict("An active operational process already uses this governed name.");
        RaiseEvent(new OperationalProcessRevisionRecorded(_tenantId, id, current.Revision + 1, content, actor,
            changedAt));
        return null;
    }

    bool Duplicate(Uuid id, OperationalProcessContent candidate) =>
        candidate.Lifecycle == TechnologyInventoryRules.Active &&
        _records.Any(other => other.Key != id &&
            other.Value.Content.Lifecycle == TechnologyInventoryRules.Active &&
            StringComparer.OrdinalIgnoreCase.Equals(other.Value.Content.Name, candidate.Name));
}
