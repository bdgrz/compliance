using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    const int MaximumDirectoryReevaluations = 1000;
    readonly Dictionary<Uuid, ServiceEngagementAcceptanceRecorded> _acceptanceSources = [];
    readonly List<DirectoryIndependenceReevaluationView> _directoryReevaluations = [];

    internal bool MatchesAcceptanceSource(ServiceEngagementAcceptanceRecorded source) =>
        _acceptanceSources.TryGetValue(source.RequestId, out var retained) &&
        IndependenceSourceDigest.AcceptanceEvent(retained) == IndependenceSourceDigest.AcceptanceEvent(source);

    internal Result<DirectoryIndependenceReevaluationView> RecordDirectoryReevaluation(
        DirectoryStatusSourceView source, ServiceEngagementAcceptanceRecorded original,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidDirectorySource(source, original) || !ValidDirectoryReceiptActor(actor))
            return Failure<DirectoryIndependenceReevaluationView>(RequestErrorKind.Conflict,
                "A directory receipt requires exact retained client acceptance and newer selected staff provenance.");
        var id = DirectoryReceiptId(source, original);
        if (_directoryReevaluations.FirstOrDefault(item => item.ReevaluationId == id) is { } prior)
            return prior.Source == source && prior.OriginalSourceSha256 == IndependenceSourceDigest.AcceptanceEvent(original)
                ? Result<DirectoryIndependenceReevaluationView>.Success(FreezeDirectoryReevaluation(prior))
                : Failure<DirectoryIndependenceReevaluationView>(RequestErrorKind.Conflict,
                    "The causal receipt identity already retains different immutable source facts.");
        var current = Acceptance(original.Acceptance.EngagementId)!;
        if (_directoryReevaluations.Count >= MaximumDirectoryReevaluations || recordedAt == default ||
            recordedAt < source.RecordedAt || recordedAt < (current.ChangedAt ?? current.RecordedAt) ||
            _services.Any(service => service.RecordedAt > recordedAt))
            return Failure<DirectoryIndependenceReevaluationView>(RequestErrorKind.Conflict,
                "Directory receipts require bounded history and chronology after the retained client and directory sources.");
        var receipt = new DirectoryIndependenceReevaluationView(id, _tenantId, source, IndependenceSourceDigest.DirectorySource(source), original.RequestId,
            IndependenceSourceDigest.AcceptanceEvent(original), FreezeAcceptedSnapshot(original.Acceptance),
            AcceptanceDigest(original.Acceptance), FreezeAcceptedSnapshot(current), AcceptanceDigest(current),
            "review_required", true, actor, recordedAt);
        var ev = new DirectoryIndependenceReevaluated(_tenantId, Sequence, receipt);
        if (!Fits(ev))
            return Failure<DirectoryIndependenceReevaluationView>(RequestErrorKind.Validation,
                "The complete directory receipt exceeds its payload bound; nothing was appended.");
        RaiseEvent(ev);
        return Result<DirectoryIndependenceReevaluationView>.Success(FreezeDirectoryReevaluation(receipt));
    }

    bool ValidDirectorySource(DirectoryStatusSourceView source, ServiceEngagementAcceptanceRecorded original) =>
        source is not null && source.EventId != Uuid.Empty && source.RequestId != Uuid.Empty &&
        source.SourceSequence > 0 && source.StaffRevision > 1 && source.RecordedAt != default &&
        source.PayloadSha256 is { Length: 64 } digest && digest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        original is not null && original.TenantId == _tenantId && MatchesAcceptanceSource(original) &&
        original.Acceptance.Assignments.Any(staff => staff.StaffMemberId == source.StaffMemberId &&
            staff.UserId == source.UserId && staff.Practice == source.Practice &&
            staff.DirectoryStaffRevision < source.StaffRevision && staff.DirectoryStaffRecordedAt <= source.RecordedAt);

    static bool ValidDirectoryReceiptActor(ActorReference? actor) => actor is
    { Kind: "system_process", Id: "reactor:FirmStaffStatusReevaluationV1" or "reactor:AcceptedStaffDirectoryReconciliationV1" } &&
        actor.Display == actor.Id["reactor:".Length..];

    static Uuid DirectoryReceiptId(DirectoryStatusSourceView source, ServiceEngagementAcceptanceRecorded original) =>
        Uuid.CreateVersion5(source.RequestId,
            $"directory_reevaluation_v1:{original.TenantId}:{original.RequestId}:{original.Acceptance.EngagementId}:{source.StaffMemberId}:{source.UserId}");

    static DirectoryIndependenceReevaluationView FreezeDirectoryReevaluation(DirectoryIndependenceReevaluationView receipt) => receipt with
    {
        OriginalAcceptance = FreezeAcceptedSnapshot(receipt.OriginalAcceptance),
        ObservedAcceptance = FreezeAcceptedSnapshot(receipt.ObservedAcceptance)
    };

    void Apply(DirectoryIndependenceReevaluated ev)
    {
        var receipt = ev.Reevaluation;
        if (receipt is null || receipt.OriginalAcceptance is null || receipt.ObservedAcceptance is null || ev.TenantId != _tenantId || ev.ExpectedSequence != Sequence ||
            !_acceptanceSources.TryGetValue(receipt.OriginalAcceptanceRequestId, out var original) ||
            !ValidDirectorySource(receipt.Source, original) ||
            receipt.SourceDescriptorSha256 != IndependenceSourceDigest.DirectorySource(receipt.Source) ||
            receipt.ReevaluationId != DirectoryReceiptId(receipt.Source, original) || receipt.TenantId != _tenantId ||
            _directoryReevaluations.Count >= MaximumDirectoryReevaluations ||
            _directoryReevaluations.Any(item => item.ReevaluationId == receipt.ReevaluationId) ||
            receipt.OriginalSourceSha256 != IndependenceSourceDigest.AcceptanceEvent(original) ||
            Intent(receipt.OriginalAcceptance) != Intent(original.Acceptance) ||
            receipt.OriginalAcceptanceSha256 != AcceptanceDigest(original.Acceptance) ||
            Acceptance(original.Acceptance.EngagementId) is not { } current ||
            Intent(receipt.ObservedAcceptance) != Intent(current) || receipt.ObservedAcceptanceSha256 != AcceptanceDigest(current) ||
            receipt.State != "review_required" || !receipt.ProductionAcceptanceBlocked ||
            !ValidDirectoryReceiptActor(receipt.Actor) ||
            receipt.RecordedAt == default || receipt.RecordedAt < receipt.Source.RecordedAt ||
            receipt.RecordedAt < (current.ChangedAt ?? current.RecordedAt) ||
            _services.Any(service => service.RecordedAt > receipt.RecordedAt) || !Fits(ev))
            throw new InvalidOperationException("Directory receipts must preserve exact source identities, snapshots, chronology and denial-only bounds.");
        var frozen = FreezeDirectoryReevaluation(receipt);
        Fence(ev.TenantId, ev.ExpectedSequence);
        _directoryReevaluations.Add(frozen);
    }
}
