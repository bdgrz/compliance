using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Retains professional duties only from signed assertions by the configured firm authority.</summary>
public sealed class FirmProfessionalDutyCatalog : Aggregate
{
    static readonly Uuid CatalogId = Uuid.Parse("c1f97f68-a902-4dc2-9e8e-b5fd28e34f5d", CultureInfo.InvariantCulture);
    readonly Dictionary<Uuid, FirmProfessionalDutyDesignationView> _designations = [];
    readonly Dictionary<Uuid, long> _activeContinuityStartRevisions = [];
    readonly Dictionary<Uuid, string> _signedAssertions = [];
    readonly HashSet<Uuid> _seenAssertions = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, FirmProfessionalDutyDesignationView Response)> _decisions = [];

    public FirmProfessionalDutyCatalog() : base(CatalogId,
        new EventStreamAddress("bdgrz", "firm-professional-duties", CatalogId.ToString()))
    {
        On<FirmProfessionalDutyDesignationRecorded>(Apply);
        On<FirmProfessionalDutyDesignationRevoked>(Apply);
    }

    public long Sequence { get; private set; }
    public IReadOnlyList<FirmProfessionalDutyDesignationView> Designations => Array.AsReadOnly(_designations.Values
        .OrderBy(item => item.DesignationId.ToString(), StringComparer.Ordinal).ToArray());
    public FirmProfessionalDutyCatalogView View() => new(Sequence, Designations);

    internal string? SignedAssertion(Uuid designationId) => _signedAssertions.GetValueOrDefault(designationId);

    internal long? ActiveContinuityStartRevision(Uuid designationId) =>
        _designations.TryGetValue(designationId, out var designation) && designation.IsActive &&
        designation.AuthorityStatus == "active" &&
        _activeContinuityStartRevisions.TryGetValue(designationId, out var revision)
            ? revision
            : null;

    public FirmProfessionalDutyDesignationView? ActiveDesignation(Uuid userId, string duty, Uuid? tenantId,
        DateTimeOffset at) => _designations.Values.SingleOrDefault(item => item.IsActive &&
            item.AuthorityStatus == "active" && item.UserId == userId && item.Duty == duty &&
            item.TenantId == tenantId && item.DesignatedAt <= at && item.NotBefore <= at && item.ExpiresAt > at);

    public bool IsDesignated(Uuid userId, string duty, Uuid? tenantId, DateTimeOffset at) =>
        userId != Uuid.Empty && IsSupportedDutyScope(duty, tenantId) &&
        ActiveDesignation(userId, duty, tenantId, at) is not null;

    internal Result<FirmProfessionalDutyDesignationView> Designate(Uuid requestId,
        VerifiedFirmProfessionalDutyAssertion evidence, string signedAssertion, FirmStaffMemberView staff,
        long expectedSequence, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidAttribution(requestId, actor, recordedAt) || !ValidAssertion(evidence, signedAssertion) ||
            evidence.Status != "active" || evidence.UserId != staff.UserId ||
            staff is not { IsActive: true, Revision: > 0 } || staff.StaffMemberId == Uuid.Empty ||
            staff.UserId == Uuid.Empty || !IsSupportedDutyScope(evidence.Duty, evidence.TenantId))
            return Failure(RequestErrorKind.Validation,
                "A duty record requires an active canonical staff identity and an active signed authority assertion.");

        var request = new RecordFirmProfessionalDutyDesignation(evidence.DesignationId, signedAssertion,
            expectedSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _seenAssertions.Contains(evidence.AssertionId))
            return Failure(RequestErrorKind.Conflict,
                "Reload the professional-duty catalog; assertion IDs and the source sequence are unique.");

        var current = _designations.GetValueOrDefault(evidence.DesignationId);
        if (current is not null)
        {
            if (current.UserId != staff.UserId || current.StaffMemberId != staff.StaffMemberId ||
                current.Duty != evidence.Duty || current.TenantId != evidence.TenantId ||
                current.AuthorityIssuer != evidence.Issuer || evidence.SupersedesAssertionId != current.AssertionId ||
                evidence.AuthorityRevision <= current.AuthorityRevision)
                return Failure(RequestErrorKind.Conflict,
                    "The signed assertion does not supersede the current authority record for this designation.");
        }
        else if (evidence.SupersedesAssertionId is not null ||
                 _designations.Values.Any(item => item.IsActive && item.UserId == staff.UserId &&
                     item.Duty == evidence.Duty && item.TenantId == evidence.TenantId))
        {
            return Failure(RequestErrorKind.Conflict,
                "The signed assertion conflicts with another active designation or names an unknown predecessor.");
        }

        var sourceReference = SourceReference(evidence.Issuer, evidence.DesignationId);
        if (!Bounded(sourceReference, 2000))
            return Failure(RequestErrorKind.Validation, "The configured authority issuer reference is invalid.");

        var designation = new FirmProfessionalDutyDesignationView(evidence.DesignationId, staff.StaffMemberId,
            staff.UserId, evidence.Duty, evidence.TenantId, sourceReference, staff.Revision, true,
            (current?.Revision ?? 0) + 1, current?.DesignatedBy ?? actor, current?.DesignatedAt ?? recordedAt,
            actor, recordedAt, current is null ? null : "Verified authority assertion refreshed",
            evidence.Issuer, evidence.AssertionId, evidence.AuthorityRevision, evidence.IssuedAt,
            evidence.NotBefore, evidence.ExpiresAt, evidence.EvidenceDigest, evidence.Status,
            evidence.SupersedesAssertionId);
        RaiseEvent(new FirmProfessionalDutyDesignationRecorded(requestId, expectedSequence, designation,
            signedAssertion));
        return Result<FirmProfessionalDutyDesignationView>.Success(designation);
    }

    internal Result<FirmProfessionalDutyDesignationView> Revoke(Uuid requestId,
        VerifiedFirmProfessionalDutyAssertion evidence, string signedAssertion, long expectedSequence,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidAttribution(requestId, actor, recordedAt) || !ValidAssertion(evidence, signedAssertion) ||
            evidence.Status != "revoked")
            return Failure(RequestErrorKind.Validation,
                "A duty revocation requires a signed revocation assertion from the configured authority.");

        var request = new RevokeFirmProfessionalDutyDesignation(evidence.DesignationId, signedAssertion,
            expectedSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _seenAssertions.Contains(evidence.AssertionId) ||
            !_designations.TryGetValue(evidence.DesignationId, out var current) || !current.IsActive ||
            current.AuthorityIssuer != evidence.Issuer || current.UserId != evidence.UserId ||
            current.Duty != evidence.Duty || current.TenantId != evidence.TenantId ||
            evidence.SupersedesAssertionId != current.AssertionId ||
            evidence.AuthorityRevision <= current.AuthorityRevision)
            return Failure(RequestErrorKind.Conflict,
                "Reload the professional-duty catalog; the signed revocation must supersede the exact active authority revision.");

        var updated = current with
        {
            IsActive = false,
            Revision = current.Revision + 1,
            AuthorityRevision = evidence.AuthorityRevision,
            AssertionId = evidence.AssertionId,
            AssertionIssuedAt = evidence.IssuedAt,
            NotBefore = evidence.NotBefore,
            ExpiresAt = evidence.ExpiresAt,
            EvidenceDigest = evidence.EvidenceDigest,
            AuthorityStatus = evidence.Status,
            SupersedesAssertionId = evidence.SupersedesAssertionId,
            LastChangedBy = actor,
            LastChangedAt = recordedAt,
            ChangeReason = "Revoked by signed authority assertion"
        };
        RaiseEvent(new FirmProfessionalDutyDesignationRevoked(requestId, expectedSequence, updated,
            signedAssertion));
        return Result<FirmProfessionalDutyDesignationView>.Success(updated);
    }

    void Apply(FirmProfessionalDutyDesignationRecorded ev)
    {
        var item = ev.Designation;
        var current = _designations.GetValueOrDefault(item.DesignationId);
        if (ev.ExpectedSequence != Sequence || !ValidView(item, ev.RequestId, current) ||
            !ValidSignedAssertionDigest(item, ev.SignedAssertion) || item.AuthorityStatus != "active" ||
            item.UserId == Uuid.Empty || item.StaffMemberId == Uuid.Empty || item.DirectoryStaffRevision <= 0 ||
            item.Revision != (current?.Revision ?? 0) + 1 || _seenAssertions.Contains(item.AssertionId) ||
            current is null && (item.SupersedesAssertionId is not null ||
                _designations.Values.Any(existing => existing.IsActive && existing.UserId == item.UserId &&
                    existing.Duty == item.Duty && existing.TenantId == item.TenantId)) ||
            current is not null && (current.UserId != item.UserId || current.StaffMemberId != item.StaffMemberId ||
                current.Duty != item.Duty || current.TenantId != item.TenantId ||
                current.AuthorityIssuer != item.AuthorityIssuer ||
                item.SupersedesAssertionId != current.AssertionId ||
                item.AuthorityRevision <= current.AuthorityRevision))
            throw new InvalidOperationException("A professional-duty event must retain a verified current assertion and exact predecessor.");

        var request = new RecordFirmProfessionalDutyDesignation(item.DesignationId, ev.SignedAssertion,
            ev.ExpectedSequence);
        // Rebuild this boundary from the ordered event stream; it is intentionally not a public event field.
        if (!ContinuesActiveAuthority(current, item) ||
            !_activeContinuityStartRevisions.ContainsKey(item.DesignationId))
            _activeContinuityStartRevisions[item.DesignationId] = item.Revision;
        _designations[item.DesignationId] = item;
        _signedAssertions[item.DesignationId] = ev.SignedAssertion;
        _seenAssertions.Add(item.AssertionId);
        _decisions.Add(ev.RequestId, (Intent(request), item.LastChangedBy, item));
        Sequence++;
    }

    void Apply(FirmProfessionalDutyDesignationRevoked ev)
    {
        var item = ev.Designation;
        if (ev.ExpectedSequence != Sequence || !_designations.TryGetValue(item.DesignationId, out var current) ||
            !current.IsActive || !ValidView(item, ev.RequestId, current) || item.IsActive ||
            item.AuthorityStatus != "revoked" || item.ChangeReason != "Revoked by signed authority assertion" ||
            item.UserId != current.UserId || item.StaffMemberId != current.StaffMemberId ||
            item.Duty != current.Duty || item.TenantId != current.TenantId ||
            item.DirectoryStaffRevision != current.DirectoryStaffRevision ||
            item.AuthorityIssuer != current.AuthorityIssuer || item.SupersedesAssertionId != current.AssertionId ||
            item.AuthorityRevision <= current.AuthorityRevision || item.Revision != current.Revision + 1 ||
            item.DesignatedBy != current.DesignatedBy || item.DesignatedAt != current.DesignatedAt ||
            _seenAssertions.Contains(item.AssertionId) || !ValidSignedAssertionDigest(item, ev.SignedAssertion))
            throw new InvalidOperationException("A professional-duty revocation must supersede one retained active designation.");

        var request = new RevokeFirmProfessionalDutyDesignation(item.DesignationId, ev.SignedAssertion,
            ev.ExpectedSequence);
        _activeContinuityStartRevisions.Remove(item.DesignationId);
        _designations[item.DesignationId] = item;
        _signedAssertions[item.DesignationId] = ev.SignedAssertion;
        _seenAssertions.Add(item.AssertionId);
        _decisions.Add(ev.RequestId, (Intent(request), item.LastChangedBy, item));
        Sequence++;
    }

    static bool ContinuesActiveAuthority(FirmProfessionalDutyDesignationView? previous,
        FirmProfessionalDutyDesignationView current) => previous is
        { IsActive: true, AuthorityStatus: "active" } &&
        previous.DesignationId == current.DesignationId && previous.UserId == current.UserId &&
        previous.StaffMemberId == current.StaffMemberId &&
        previous.DirectoryStaffRevision == current.DirectoryStaffRevision &&
        previous.Duty == current.Duty && previous.TenantId == current.TenantId &&
        previous.AuthorityIssuer == current.AuthorityIssuer &&
        previous.AssertionIssuedAt <= previous.LastChangedAt && previous.NotBefore <= previous.LastChangedAt &&
        previous.ExpiresAt > current.LastChangedAt && current.AssertionIssuedAt <= current.LastChangedAt &&
        current.NotBefore <= current.LastChangedAt;

    Result<FirmProfessionalDutyDesignationView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor == actor
                ? Result<FirmProfessionalDutyDesignationView>.Success(previous.Response)
                : Failure(RequestErrorKind.Conflict,
                    "The request identity already retains another professional-duty intent or actor.")
            : null;

    static bool ValidAttribution(Uuid requestId, ActorReference actor, DateTimeOffset recordedAt) =>
        IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator");

    static bool ValidAssertion(VerifiedFirmProfessionalDutyAssertion evidence, string signedAssertion) =>
        evidence is not null && evidence.AssertionId != Uuid.Empty &&
        evidence.DesignationId != Uuid.Empty && evidence.UserId != Uuid.Empty &&
        evidence.AuthorityRevision > 0 && evidence.IssuedAt != default && evidence.NotBefore != default &&
        evidence.ExpiresAt > evidence.NotBefore && !string.IsNullOrWhiteSpace(evidence.Issuer) &&
        !string.IsNullOrWhiteSpace(evidence.Audience) && Bounded(signedAssertion, 16_384) &&
        ValidDigest(evidence.EvidenceDigest) && EvidenceDigest(signedAssertion) == evidence.EvidenceDigest;

    static bool ValidView(FirmProfessionalDutyDesignationView item, Uuid requestId,
        FirmProfessionalDutyDesignationView? current = null) =>
        item.DesignationId != Uuid.Empty && item.AssertionId != Uuid.Empty && item.AuthorityRevision > 0 &&
        item.AuthorityStatus is "active" or "revoked" && item.IsActive == (item.AuthorityStatus == "active") &&
        item.ExpiresAt > item.NotBefore &&
        item.AssertionIssuedAt != default && item.NotBefore != default &&
        item.AuthorityIssuer.Length > 0 && ValidDigest(item.EvidenceDigest) &&
        ValidAttribution(requestId, item.LastChangedBy, item.LastChangedAt) &&
        ValidAttribution(requestId, item.DesignatedBy, item.DesignatedAt) &&
        (current is null
            ? item.LastChangedBy == item.DesignatedBy && item.LastChangedAt == item.DesignatedAt &&
                item.ChangeReason is null
            : item.LastChangedAt >= current.LastChangedAt && item.ChangeReason is not null &&
                Bounded(item.ChangeReason, 2000));

    static bool ValidSignedAssertionDigest(FirmProfessionalDutyDesignationView item, string signedAssertion) =>
        Bounded(signedAssertion, 16_384) && EvidenceDigest(signedAssertion) == item.EvidenceDigest;

    static bool TryReadAssertionDigest(string signedAssertion, out string digest)
    {
        digest = string.Empty;
        if (!Bounded(signedAssertion, 16_384))
            return false;
        digest = EvidenceDigest(signedAssertion);
        return true;
    }

    static bool IsSupportedDutyScope(string duty, Uuid? tenantId) => duty switch
    {
        FirmProfessionalDuty.EngagementPartner => tenantId is { } tenant && tenant != Uuid.Empty,
        FirmProfessionalDuty.RuleRatifier => tenantId is null,
        _ => false
    };

    static string SourceReference(string issuer, Uuid designationId) =>
        $"{issuer.TrimEnd('/')}/professional-duties/{designationId}";

    static string EvidenceDigest(string signedAssertion) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signedAssertion)));

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static Result<FirmProfessionalDutyDesignationView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(kind, message));
}
