using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Owns supplied tenant text and immutable per-entry license revisions for one edition.</summary>
public sealed class CriteriaTextOverlayLedger : Aggregate
{
    public const string Area = "criteria-text-overlays";
    readonly Uuid _tenantId;
    readonly Uuid _editionId;
    readonly Dictionary<string, CriteriaTextOverlayRevision> _entries = new(StringComparer.Ordinal);
    readonly Dictionary<Uuid, (string Identifier, long? ExpectedRevision, CriteriaTextOverlayContent Content,
        long Revision)> _decisions = [];

    public CriteriaTextOverlayLedger(Uuid tenantId, Uuid editionId)
        : base(editionId, new EventStreamAddress(tenantId.ToString(), Area, editionId.ToString()))
    {
        _tenantId = tenantId;
        _editionId = editionId;
        On<CriteriaTextOverlayEntryRevised>(Apply);
    }

    public CriteriaTextOverlayRevision? Get(string identifier) => _entries.GetValueOrDefault(identifier);

    public Result<CriteriaTextOverlayRegistration> Set(string identifier, Uuid requestId,
        long? expectedRevision, CriteriaTextOverlayContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (_tenantId == Uuid.Empty || _editionId == Uuid.Empty || requestId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(identifier) || identifier.Length > 200 || identifier.Any(char.IsControl) ||
            content is null || string.IsNullOrWhiteSpace(content.Text) || content.Text.Length > 16000 ||
            content.Text.Any(static value => char.IsControl(value) && value is not ('\r' or '\n' or '\t')) ||
            string.IsNullOrWhiteSpace(content.Supplier) || content.Supplier.Length > 200 || content.Supplier.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(content.LicenseReference) || content.LicenseReference.Length > 2000 ||
            content.LicenseReference.Any(char.IsControl) || content.UsageFlags is null ||
            actor.Kind != "member" || string.IsNullOrWhiteSpace(actor.Display))
            return Failure(RequestErrorKind.Validation, "An overlay requires bounded supplied text, supplier, license and member attribution.");
        content = content with
        {
            Text = content.Text.Trim(),
            Supplier = content.Supplier.Trim(),
            LicenseReference = content.LicenseReference.Trim()
        };
        if (_decisions.TryGetValue(requestId, out var decision))
            return decision.Identifier == identifier && decision.ExpectedRevision == expectedRevision && decision.Content == content
                ? Success(identifier, decision.Revision)
                : Failure(RequestErrorKind.Conflict, "The overlay request identity already records a different intent.");
        var current = Get(identifier);
        if (expectedRevision != current?.Revision)
            return Failure(RequestErrorKind.Conflict,
                $"Reload the overlay before revising. Current revision: {current?.Revision ?? 0}.");
        var revised = new CriteriaTextOverlayEntryRevised(_tenantId, _editionId, identifier, requestId,
            (current?.Revision ?? 0) + 1, content, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.CriteriaTextOverlayEntryRevised).Length > 48 * 1024)
            return Failure(RequestErrorKind.Validation, "The supplied overlay exceeds the bounded event payload.");
        RaiseEvent(revised);
        return Success(identifier, revised.Revision);
    }

    void Apply(CriteriaTextOverlayEntryRevised revised)
    {
        if (revised.TenantId != _tenantId || revised.EditionId != _editionId ||
            revised.Revision != (Get(revised.Identifier)?.Revision ?? 0) + 1)
            throw new InvalidOperationException("An overlay revision must follow its owning tenant, edition and predecessor.");
        _entries[revised.Identifier] = new CriteriaTextOverlayRevision(_tenantId, _editionId, revised.Identifier,
            revised.Revision, revised.Content, revised.Actor, revised.RecordedAt);
        _decisions.Add(revised.RequestId, (revised.Identifier,
            revised.Revision == 1 ? null : revised.Revision - 1, revised.Content, revised.Revision));
    }

    Result<CriteriaTextOverlayRegistration> Success(string identifier, long revision) =>
        Result<CriteriaTextOverlayRegistration>.Success(new CriteriaTextOverlayRegistration(_editionId, identifier, revision));

    static Result<CriteriaTextOverlayRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CriteriaTextOverlayRegistration>.Failure(new RequestError(kind, message));
}
