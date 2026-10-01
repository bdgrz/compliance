using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     One program's evidence requests in one stream, with optimistic concurrency per request. A request is open
///     until an artifact fulfils it or it is cancelled with a rationale; both are attributed and final.
/// </summary>
public sealed class EvidenceRequestLedger : Aggregate
{
    public const string Open = "open";
    public const string Fulfilled = "fulfilled";
    public const string Cancelled = "cancelled";
    const int MaximumTitleLength = 200;
    const int MaximumTextLength = 4000;

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Dictionary<Uuid, EvidenceRequestView> _requests = [];
    readonly List<Uuid> _order = [];

    public EvidenceRequestLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "evidence-requests", programId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<EvidenceRequestOpened>(ev =>
        {
            _requests[ev.EvidenceRequestId] = new EvidenceRequestView(ev.TenantId, ev.ProgramId,
                ev.EvidenceRequestId, 1, ev.Title, ev.Instructions, ev.OwnerMemberId, ev.DueOn, ev.ControlId, Open,
                ev.RequestedBy, ev.OpenedAt, null, null, null, null, null, null);
            _order.Add(ev.EvidenceRequestId);
        });
        On<EvidenceRequestFulfilled>(ev => _requests[ev.EvidenceRequestId] = _requests[ev.EvidenceRequestId] with
        {
            Revision = ev.Revision,
            Status = Fulfilled,
            ArtifactId = ev.ArtifactId,
            FulfilledBy = ev.FulfilledBy,
            FulfilledAt = ev.FulfilledAt,
        });
        On<EvidenceRequestCancelled>(ev => _requests[ev.EvidenceRequestId] = _requests[ev.EvidenceRequestId] with
        {
            Revision = ev.Revision,
            Status = Cancelled,
            CancellationRationale = ev.Rationale,
            CancelledBy = ev.CancelledBy,
            CancelledAt = ev.CancelledAt,
        });
    }

    public EvidenceRequestView? Find(Uuid requestId) => _requests.GetValueOrDefault(requestId);

    public IReadOnlyList<EvidenceRequestView> ReadAll() => [.. _order.Select(id => _requests[id])];

    public CommandFailure? OpenRequest(Uuid requestId, string title, string instructions, Uuid ownerMemberId,
        DateOnly dueOn, Uuid? controlId, ActorReference requestedBy, DateTimeOffset openedAt)
    {
        var cleanTitle = title?.Trim() ?? "";
        var cleanInstructions = instructions?.Trim() ?? "";
        if (cleanTitle.Length is 0 or > MaximumTitleLength)
            return CommandFailure.InvalidContent("An evidence request requires a title of at most 200 characters.");
        if (cleanInstructions.Length is 0 or > MaximumTextLength)
            return CommandFailure.InvalidContent("An evidence request requires instructions of at most 4000 characters.");
        if (ownerMemberId == Uuid.Empty || controlId == Uuid.Empty)
            return CommandFailure.InvalidContent("An evidence request requires an owner.");
        if (dueOn < DateOnly.FromDateTime(openedAt.UtcDateTime))
            return CommandFailure.InvalidContent("An evidence request cannot be due in the past.");
        if (_requests.TryGetValue(requestId, out var existing))
            return existing.Title == cleanTitle && existing.Instructions == cleanInstructions &&
                   existing.OwnerMemberId == ownerMemberId && existing.DueOn == dueOn &&
                   existing.ControlId == controlId
                ? null
                : CommandFailure.StateConflict("The evidence request already exists with different details.");
        RaiseEvent(new EvidenceRequestOpened(_tenantId, _programId, requestId, cleanTitle, cleanInstructions,
            ownerMemberId, dueOn, controlId, requestedBy, openedAt));
        return null;
    }

    public CommandFailure? Fulfil(Uuid requestId, long expectedRevision, Uuid artifactId,
        ActorReference fulfilledBy, DateTimeOffset fulfilledAt)
    {
        if (RequireOpen(requestId, expectedRevision) is { } failure)
            return failure;
        if (artifactId == Uuid.Empty)
            return CommandFailure.InvalidContent("Fulfilment requires an evidence artifact.");
        RaiseEvent(new EvidenceRequestFulfilled(_tenantId, _programId, requestId, expectedRevision + 1, artifactId,
            fulfilledBy, fulfilledAt));
        return null;
    }

    public CommandFailure? Cancel(Uuid requestId, long expectedRevision, string rationale,
        ActorReference cancelledBy, DateTimeOffset cancelledAt)
    {
        if (RequireOpen(requestId, expectedRevision) is { } failure)
            return failure;
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > MaximumTextLength)
            return CommandFailure.InvalidContent("Cancelling an evidence request requires a rationale.");
        RaiseEvent(new EvidenceRequestCancelled(_tenantId, _programId, requestId, expectedRevision + 1,
            rationale.Trim(), cancelledBy, cancelledAt));
        return null;
    }

    CommandFailure? RequireOpen(Uuid requestId, long expectedRevision)
    {
        if (!_requests.TryGetValue(requestId, out var request))
            return CommandFailure.MissingRecord("The evidence request was not found.");
        if (request.Revision != expectedRevision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence request", request.Revision));
        return request.Status == Open
            ? null
            : CommandFailure.StateConflict($"The evidence request is already {request.Status}.");
    }
}
