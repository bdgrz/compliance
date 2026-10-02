using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Owns a tenant's location inventory: stable IDs, active-name uniqueness, and attributed
///     complete revisions.
/// </summary>
public sealed class LocationRegister : Aggregate
{
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, (long Revision, LocationContent Content)> _records = [];

    public LocationRegister(Uuid tenantId)
        : base(tenantId, InventoryRegisters.Address(tenantId, InventoryRegisters.Locations))
    {
        _tenantId = tenantId;
        On<LocationRevisionRecorded>(ev =>
        {
            if (ev.TenantId != _tenantId ||
                ev.Revision != (_records.TryGetValue(ev.LocationId, out var known) ? known.Revision : 0) + 1)
                throw new InvalidOperationException(
                    "A location revision must follow its tenant and previous revision.");
            _records[ev.LocationId] = (ev.Revision, ev.Content);
        });
    }

    public long RevisionOf(Uuid id) => _records.TryGetValue(id, out var known) ? known.Revision : 0;

    public LocationContent? Get(Uuid id) =>
        _records.TryGetValue(id, out var known) ? known.Content : null;

    public Result<LocationRegistration> Record(Uuid id, LocationContent requested, ActorReference actor,
        DateTimeOffset changedAt)
    {
        var content = InventoryRegisterRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = id == Uuid.Empty ? "A location requires a nonempty identity." :
            InventoryRegisterRules.Validate(content);
        if (error is not null)
            return Result<LocationRegistration>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (_records.TryGetValue(id, out var existing))
            return existing.Revision == 1 && existing.Content == content
                ? Result<LocationRegistration>.Success(new LocationRegistration(id))
                : Result<LocationRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The location already exists with different content."));
        if (Duplicate(id, content))
            return Result<LocationRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An active location of this kind already uses this governed name."));
        RaiseEvent(new LocationRevisionRecorded(_tenantId, id, 1, content, actor, changedAt));
        return Result<LocationRegistration>.Success(new LocationRegistration(id));
    }

    public CommandFailure? Revise(Uuid id, long expectedRevision,
        Func<LocationContent, LocationContent> change, ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_records.TryGetValue(id, out var current))
            return CommandFailure.MissingRecord("The location was not found.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("location",
                current.Revision));
        var content = InventoryRegisterRules.Normalize(change(current.Content));
        var error = InventoryRegisterRules.Validate(content) ??
                    (content.Kind != current.Content.Kind
                        ? "A location's kind is immutable."
                        : null);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        if (Duplicate(id, content))
            return CommandFailure.StateConflict("An active location of this kind already uses this governed name.");
        RaiseEvent(new LocationRevisionRecorded(_tenantId, id, current.Revision + 1, content, actor,
            changedAt));
        return null;
    }

    bool Duplicate(Uuid id, LocationContent candidate) =>
        candidate.Lifecycle == TechnologyInventoryRules.Active &&
        _records.Any(other => other.Key != id &&
            other.Value.Content.Lifecycle == TechnologyInventoryRules.Active &&
            other.Value.Content.Kind == candidate.Kind &&
            StringComparer.OrdinalIgnoreCase.Equals(other.Value.Content.Name, candidate.Name));
}
