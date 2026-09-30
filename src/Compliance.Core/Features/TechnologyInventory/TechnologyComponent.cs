using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Owns one manually inventoried technology component and its attributed revisions.</summary>
public sealed class TechnologyComponent : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    TechnologyComponentContent? _content;

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public TechnologyComponentContent? Content => IsCreated ? _content : null;

    public TechnologyComponent(Uuid tenantId, Uuid id)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        On<TechnologyComponentRevisionRecorded>(ev =>
        {
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<InformationAssetRevisionRecorded>(_ => _foreign = true);
        On<DataFlowRevisionRecorded>(_ => _foreign = true);
    }

    public Result<TechnologyComponentRegistration> Record(TechnologyComponentContent requested, ActorReference actor,
        DateTimeOffset changedAt)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.Category == TechnologyInventoryRules.CloudAccount &&
                     content.SystemInstanceId != Id
                        ? "A cloud_account component shares its system instance identity."
                        : null);
        if (error is not null)
            return Result<TechnologyComponentRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                error));
        if (_foreign)
            return Result<TechnologyComponentRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        if (_created)
            return _content == content
                ? Result<TechnologyComponentRegistration>.Success(new TechnologyComponentRegistration(Id))
                : Result<TechnologyComponentRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The technology component already exists with different content."));
        RaiseEvent(new TechnologyComponentRevisionRecorded(_tenantId, Id, 1, content, actor, changedAt));
        return Result<TechnologyComponentRegistration>.Success(new TechnologyComponentRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, Func<TechnologyComponentContent, TechnologyComponentContent> change,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The technology component was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("technology component",
                _revision));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.Category != _content!.Category ||
                     content.SystemInstanceId != _content.SystemInstanceId
                        ? "A component's category and system instance are immutable."
                        : null);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        RaiseEvent(new TechnologyComponentRevisionRecorded(_tenantId, Id, _revision + 1, content, actor, changedAt));
        return null;
    }
}
