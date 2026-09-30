using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Owns one manually inventoried information asset and its attributed revisions.</summary>
public sealed class InformationAsset : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    bool _foreign;
    long _revision;
    InformationAssetContent? _content;

    public bool IsCreated => _created && !_foreign;
    public long Revision => _revision;
    public InformationAssetContent? Content => IsCreated ? _content : null;

    public InformationAsset(Uuid tenantId, Uuid id)
        : base(id, TechnologyInventoryStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        On<InformationAssetRevisionRecorded>(ev =>
        {
            _created = true;
            _revision = ev.Revision;
            _content = ev.Content;
        });
        On<TechnologyComponentRevisionRecorded>(_ => _foreign = true);
        On<DataFlowRevisionRecorded>(_ => _foreign = true);
    }

    public Result<InformationAssetRegistration> Record(InformationAssetContent requested, ActorReference actor,
        DateTimeOffset changedAt)
    {
        var content = TechnologyInventoryRules.Normalize(requested with
        {
            Lifecycle = TechnologyInventoryRules.Active,
        });
        var error = TechnologyInventoryRules.Validate(content);
        if (error is not null)
            return Result<InformationAssetRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                error));
        if (_foreign)
            return Result<InformationAssetRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity belongs to another inventory record."));
        if (_created)
            return _content == content
                ? Result<InformationAssetRegistration>.Success(new InformationAssetRegistration(Id))
                : Result<InformationAssetRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The information asset already exists with different content."));
        RaiseEvent(new InformationAssetRevisionRecorded(_tenantId, Id, 1, content, actor, changedAt));
        return Result<InformationAssetRegistration>.Success(new InformationAssetRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, Func<InformationAssetContent, InformationAssetContent> change,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The information asset was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("information asset",
                _revision));
        var content = TechnologyInventoryRules.Normalize(change(_content!));
        var error = TechnologyInventoryRules.Validate(content);
        if (error is not null)
            return CommandFailure.InvalidContent(error);
        RaiseEvent(new InformationAssetRevisionRecorded(_tenantId, Id, _revision + 1, content, actor, changedAt));
        return null;
    }
}
