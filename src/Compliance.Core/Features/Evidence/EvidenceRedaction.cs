using System.Text.Json;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Retains declared derivative lineage; it never transforms or discloses content.</summary>
public sealed class EvidenceRedaction : Aggregate
{
    const int MaximumHistory = 100;
    readonly Uuid _tenantId;
    readonly List<EvidenceRedactionPreparationFact> _preparations = [];
    readonly List<EvidenceRedactionApprovalFact> _approvals = [];
    DateTimeOffset _lastChangedAt;

    public long Revision { get; private set; }
    public EvidenceRedactionPreparationFact? CurrentPreparation => _preparations.LastOrDefault();
    public IReadOnlyList<EvidenceRedactionPreparationFact> Preparations => Array.AsReadOnly(_preparations.ToArray());
    public IReadOnlyList<EvidenceRedactionApprovalFact> Approvals => Array.AsReadOnly(_approvals.ToArray());
    public EvidenceRedactionApprovalFact? CurrentApproval => _approvals.LastOrDefault(approval => approval.PreparationId == CurrentPreparation?.PreparationId);

    public EvidenceRedaction(Uuid tenantId, Uuid redactionId)
        : base(redactionId, new EventStreamAddress(tenantId.ToString(), "evidence-redactions", redactionId.ToString()))
    {
        _tenantId = tenantId;
        On<EvidenceRedactionPrepared>(Apply);
        On<EvidenceRedactionApproved>(Apply);
    }

    internal Result<EvidenceRedactionPreparationFact> Prepare(Uuid requestId, long expectedRevision,
        EvidenceRedactionSourceCapsule original, EvidenceRedactionSourceCapsule derived,
        string provenance, string reason, ActorReference actor, DateTimeOffset preparedAt)
    {
        provenance = provenance?.Trim() ?? "";
        reason = reason?.Trim() ?? "";
        if (!ValidIdentity(original) || !ValidIdentity(derived) || original.ArtifactId == derived.ArtifactId ||
            original.ContentSha256 == derived.ContentSha256 || !ValidActor(actor) || requestId == Uuid.Empty ||
            provenance.Length is 0 or > 4000 || reason.Length is 0 or > 4000)
            return Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Validation,
                "Redaction preparation requires distinct immutable artifact identities, attribution and bounded provenance/reason.");
        if (_preparations.FirstOrDefault(item => item.PreparationId == requestId) is { } prior)
            return expectedRevision == prior.Revision - 1 && prior.Original == original && prior.Derived == derived &&
                prior.Provenance == provenance && prior.Reason == reason && SameActor(prior.PreparedBy, actor)
                ? Result<EvidenceRedactionPreparationFact>.Success(prior)
                : Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Conflict,
                    "This preparation identity retains different intent or actor.");
        if (_approvals.Any(item => item.ApprovalId == requestId) || expectedRevision != Revision || _preparations.Count + _approvals.Count >= MaximumHistory ||
            _preparations.FirstOrDefault() is { } first && (first.Original != original || first.Derived != derived) ||
            preparedAt == default || preparedAt < original.RegisteredAt || preparedAt < derived.RegisteredAt ||
            preparedAt < _lastChangedAt)
            return Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Conflict,
                "Reload the exact source-bound redaction revision; history and source chronology must be preserved.");
        var view = new EvidenceRedactionPreparationFact(requestId, Revision + 1, original, derived,
            provenance, reason, actor, preparedAt);
        var ev = new EvidenceRedactionPrepared(_tenantId, Id, Revision, view);
        if (!Fits(ev))
            return Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Validation, "The complete redaction event exceeds its payload bound.");
        RaiseEvent(ev);
        return Result<EvidenceRedactionPreparationFact>.Success(view);
    }

    internal Result<EvidenceRedactionApprovalFact> Approve(Uuid requestId, long expectedRevision,
        Uuid preparationId, long preparedRevision, ulong derivedSourcePosition,
        ActorReference actor, DateTimeOffset approvedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (!ValidActor(actor) || requestId == Uuid.Empty)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Validation, "Approval requires a canonical attributed member decision.");
        if (_approvals.FirstOrDefault(item => item.ApprovalId == requestId) is { } prior)
            return expectedRevision == prior.Revision - 1 && prior.PreparationId == preparationId &&
                prior.PreparedRevision == preparedRevision && SameActor(prior.ApprovedBy, actor) &&
                prior.SeparationOfDutiesWaiver?.WaiverId == waiver?.Id
                ? Result<EvidenceRedactionApprovalFact>.Success(prior)
                : Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Conflict, "This approval identity retains different intent or canonical actor.");
        if (_preparations.Any(item => item.PreparationId == requestId) || expectedRevision != Revision ||
            CurrentPreparation is not { } prepared || prepared.PreparationId != preparationId || prepared.Revision != preparedRevision ||
            CurrentApproval is not null || derivedSourcePosition == 0 || _preparations.Count + _approvals.Count >= MaximumHistory ||
            approvedAt == default || approvedAt < _lastChangedAt)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Conflict, "Approve the current exact preparation and preserve bounded source chronology.");
        var waiverSnapshot = waiver is null ? null : SnapshotWaiver(waiver);
        if (waiver is not null && (waiver.TenantId != _tenantId || waiverSnapshot is null ||
                !ValidWaiver(waiverSnapshot, prepared, actor, approvedAt)) ||
            SameActor(prepared.PreparedBy, actor) && waiverSnapshot is null)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Forbidden, "A preparer cannot approve their own derivative without an independently approved exact-scope active waiver.");
        var view = new EvidenceRedactionApprovalFact(requestId, Revision + 1, preparationId, preparedRevision,
            derivedSourcePosition, actor, approvedAt, waiverSnapshot);
        var ev = new EvidenceRedactionApproved(_tenantId, Id, Revision, view);
        if (!Fits(ev))
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Validation, "The complete approval event exceeds its payload bound.");
        RaiseEvent(ev);
        return Result<EvidenceRedactionApprovalFact>.Success(view);
    }

    void Apply(EvidenceRedactionApproved ev)
    {
        var view = ev.Approval;
        if (ev.TenantId != _tenantId || ev.RedactionId != Id || ev.ExpectedRevision != Revision ||
            view is null || view.Revision != Revision + 1 || view.ApprovalId == Uuid.Empty || !ValidActor(view.ApprovedBy) ||
            _preparations.Any(item => item.PreparationId == view.ApprovalId) || _approvals.Any(item => item.ApprovalId == view.ApprovalId) ||
            CurrentPreparation is not { } prepared || prepared.PreparationId != view.PreparationId || prepared.Revision != view.PreparedRevision ||
            CurrentApproval is not null || view.DerivedSourcePosition == 0 || _preparations.Count + _approvals.Count >= MaximumHistory ||
            view.ApprovedAt == default || view.ApprovedAt < _lastChangedAt ||
            view.SeparationOfDutiesWaiver is { } waiver && !ValidWaiver(waiver, prepared, view.ApprovedBy, view.ApprovedAt) ||
            SameActor(prepared.PreparedBy, view.ApprovedBy) && view.SeparationOfDutiesWaiver is null || !Fits(ev))
            throw new InvalidOperationException("Redaction approvals must retain the exact current preparation, attribution, chronology and independent decision.");
        _approvals.Add(view);
        Revision = view.Revision;
        _lastChangedAt = view.ApprovedAt;
    }

    static EvidenceRedactionWaiverSnapshot? SnapshotWaiver(SeparationOfDutiesWaiver waiver) =>
        waiver.Scope is { } scope && waiver.ApproverMemberId is { } approver && waiver.ApprovedAt is { } approvedAt
            ? new(waiver.TenantId, waiver.Id, scope, waiver.BeneficiaryMemberId, waiver.RequesterMemberId,
                approver, waiver.RequestedAt, approvedAt, waiver.ExpiresAt)
            : null;

    bool ValidWaiver(EvidenceRedactionWaiverSnapshot waiver, EvidenceRedactionPreparationFact prepared,
        ActorReference actor, DateTimeOffset at) => waiver.TenantId == _tenantId && waiver.WaiverId != Uuid.Empty &&
        waiver.Scope == WaiverScope(prepared) && waiver.BeneficiaryMemberId.ToString() == actor.Id &&
        waiver.RequesterMemberId != Uuid.Empty && waiver.ApproverMemberId != Uuid.Empty &&
        waiver.ApproverMemberId != waiver.BeneficiaryMemberId && waiver.ApproverMemberId != waiver.RequesterMemberId &&
        waiver.RequestedAt >= prepared.PreparedAt && waiver.ApprovedAt >= waiver.RequestedAt &&
        at >= waiver.ApprovedAt && at < waiver.ExpiresAt;

    internal SeparationOfDutiesWaiverScope WaiverScope(EvidenceRedactionPreparationFact prepared) =>
        new("evidence_redaction", Id, prepared.PreparationId, prepared.Revision, SeparationOfDutiesActions.Approve);

    static bool SameActor(ActorReference first, ActorReference second) => first.Kind == second.Kind && first.Id == second.Id;

    void Apply(EvidenceRedactionPrepared ev)
    {
        var view = ev.Preparation;
        if (ev.TenantId != _tenantId || ev.RedactionId != Id || ev.ExpectedRevision != Revision ||
            view is null || view.Revision != Revision + 1 || view.PreparationId == Uuid.Empty ||
            !ValidIdentity(view.Original) || !ValidIdentity(view.Derived) ||
            view.Original.ArtifactId == view.Derived.ArtifactId || view.Original.ContentSha256 == view.Derived.ContentSha256 ||
            !ValidActor(view.PreparedBy) || view.Provenance is not { Length: > 0 and <= 4000 } ||
            view.Reason is not { Length: > 0 and <= 4000 } || view.Provenance != view.Provenance.Trim() || view.Reason != view.Reason.Trim() ||
            _preparations.Count + _approvals.Count >= MaximumHistory || _preparations.Any(item => item.PreparationId == view.PreparationId) || _approvals.Any(item => item.ApprovalId == view.PreparationId) ||
            _preparations.FirstOrDefault() is { } first && (first.Original != view.Original || first.Derived != view.Derived) ||
            view.PreparedAt == default || view.PreparedAt < view.Original.RegisteredAt || view.PreparedAt < view.Derived.RegisteredAt ||
            view.PreparedAt < _lastChangedAt || !Fits(ev))
            throw new InvalidOperationException("Redaction lineage must retain exact distinct source identities, attribution, chronology and bounded history.");
        _preparations.Add(view);
        Revision = view.Revision;
        _lastChangedAt = view.PreparedAt;
    }

    bool ValidIdentity(EvidenceRedactionSourceCapsule? source) => _tenantId != Uuid.Empty && Id != Uuid.Empty &&
        source is not null && source.TenantId == _tenantId && source.RegistrationEventId != Uuid.Empty &&
        ValidDigest(source.ContentSha256) && ValidDigest(source.RegistrationSha256) && source.ContentLength > 0 &&
        source.ArtifactId != Uuid.Empty && source.RegisteredAt != default &&
        source.IdentitySha256 == IdentityDigest(source);

    internal static string IdentityDigest(EvidenceRedactionSourceCapsule source) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source with { IdentitySha256 = "" },
            typeof(EvidenceRedactionSourceCapsule), ComplianceCoreJsonContext.Default)));

    internal EvidenceRedactionView PublicView(string originalState, string derivedState, bool approvalCurrent) =>
        new(_tenantId, Id, Revision,
            Array.AsReadOnly(_preparations.Select(prepared => new EvidenceRedactionPreparationView(
                prepared.PreparationId, prepared.Revision, PublicIdentity(prepared.Original), PublicIdentity(prepared.Derived),
                prepared.Provenance, prepared.Reason, prepared.PreparedBy, prepared.PreparedAt)).ToArray()),
            Array.AsReadOnly(_approvals.Select(approval => new EvidenceRedactionApprovalView(
                approval.ApprovalId, approval.Revision, approval.PreparationId, approval.PreparedRevision,
                approval.ApprovedBy, approval.ApprovedAt, approval.SeparationOfDutiesWaiver is not null,
                approval.SeparationOfDutiesWaiver?.WaiverId)).ToArray()), originalState, derivedState, approvalCurrent);

    static EvidenceRedactionArtifactView PublicIdentity(EvidenceRedactionSourceCapsule source) =>
        new(source.ArtifactId, source.ContentSha256, source.ContentLength, source.RegisteredAt);

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static bool ValidActor(ActorReference? actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var id) && id != Uuid.Empty;
    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;
    static Result<T> Refuse<T>(RequestErrorKind kind, string reason) => Result<T>.Failure(new RequestError(kind, reason));
}
