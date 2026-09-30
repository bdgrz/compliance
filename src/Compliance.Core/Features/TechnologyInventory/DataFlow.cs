using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Owns one manually inventoried data flow and its attributed revisions.</summary>
public sealed class DataFlow : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    DataFlowContent? _content;

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public DataFlowContent? Content => IsCreated ? _content : null;

    public DataFlow(Uuid tenantId, Uuid id)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        On<DataFlowRevisionRecorded>(ev =>
        {
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<TechnologyComponentRevisionRecorded>(_ => _foreign = true);
        On<InformationAssetRevisionRecorded>(_ => _foreign = true);
    }

    public Result<DataFlowRegistration> Record(DataFlowContent requested, ActorReference actor,
        DateTimeOffset changedAt)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content);
        if (error is not null)
            return Result<DataFlowRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                error));
        if (_foreign)
            return Result<DataFlowRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        if (_created)
            return TechnologyInventoryRules.SameContent(_content!, content)
                ? Result<DataFlowRegistration>.Success(new DataFlowRegistration(Id))
                : Result<DataFlowRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The data flow already exists with different content."));
        RaiseEvent(new DataFlowRevisionRecorded(_tenantId, Id, 1, content, actor, changedAt));
        return Result<DataFlowRegistration>.Success(new DataFlowRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, Func<DataFlowContent, DataFlowContent> change,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The data flow was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("data flow",
                _revision));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        var error = TechnologyInventoryRules.Validate(content) ??
                    (content.EffectiveFrom < _content!.EffectiveFrom
                        ? "A data flow version cannot take effect before its predecessor."
                        : null);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        RaiseEvent(new DataFlowRevisionRecorded(_tenantId, Id, _revision + 1, content, actor, changedAt));
        return null;
    }
}
