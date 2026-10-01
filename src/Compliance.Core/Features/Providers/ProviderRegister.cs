using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Owns tenant provider names, attributed revisions and immutable request decisions.</summary>
public sealed class ProviderRegister : Aggregate
{
    public const string Area = "providers";
    public const int MaximumPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, ProviderView> _providers = [];
    readonly Dictionary<Uuid, (Uuid ProviderId, long? ExpectedRevision, ProviderContent Content, long Revision)> _decisions = [];

    public ProviderRegister(Uuid tenantId)
        : base(tenantId, new EventStreamAddress(tenantId.ToString(), Area, tenantId.ToString()))
    {
        _tenantId = tenantId;
        On<ProviderRecorded>(ev => Apply(ev.TenantId, ev.ProviderId, ev.RequestId, 1, ev.Content, ev.Actor, ev.RecordedAt));
        On<ProviderRevised>(ev => Apply(ev.TenantId, ev.ProviderId, ev.RequestId, ev.Revision, ev.Content, ev.Actor, ev.RecordedAt));
    }

    public ProviderView? Get(Uuid providerId) => _providers.GetValueOrDefault(providerId);

    /// <summary>Checks retained authored input before a handler resolves mutable external sources.</summary>
    public Result<ProviderRegistration>? CheckRetry(Uuid providerId, Uuid requestId,
        long? expectedRevision, ProviderContent content)
    {
        if (ProviderRules.InputError(content) is { } inputError)
            return Failure(RequestErrorKind.Validation, inputError);
        if (!_decisions.TryGetValue(requestId, out var decision))
            return null;
        return decision.ProviderId == providerId && decision.ExpectedRevision == expectedRevision &&
               SameIntent(decision.Content, ProviderRules.Normalize(content))
            ? Result<ProviderRegistration>.Success(new ProviderRegistration(providerId, decision.Revision))
            : Failure(RequestErrorKind.Conflict, "The request already has a different retained provider decision.");
    }

    public Result<ProviderRegistration> Record(Uuid providerId, Uuid requestId,
        ProviderContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (ProviderRules.InputError(content) is { } inputError)
            return Failure(RequestErrorKind.Validation, inputError);
        content = ProviderRules.Normalize(content);
        if (CheckRetry(providerId, requestId, null, content) is { } retry)
            return retry;
        if (Validate(providerId, requestId, content) is { } error)
            return Result<ProviderRegistration>.Failure(error);
        if (_providers.ContainsKey(providerId))
            return Failure(RequestErrorKind.Conflict, "The provider identity already exists.");
        var ev = new ProviderRecorded(_tenantId, providerId, requestId, content, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.ProviderRecorded).Length > MaximumPayloadBytes)
            return Failure(RequestErrorKind.Validation, "The provider declaration exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<ProviderRegistration>.Success(new ProviderRegistration(providerId, 1));
    }

    public Result<ProviderRegistration> Revise(Uuid providerId, Uuid requestId,
        long expectedRevision, ProviderContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (ProviderRules.InputError(content) is { } inputError)
            return Failure(RequestErrorKind.Validation, inputError);
        content = ProviderRules.Normalize(content);
        if (CheckRetry(providerId, requestId, expectedRevision, content) is { } retry)
            return retry;
        if (!_providers.TryGetValue(providerId, out var current))
            return Failure(RequestErrorKind.NotFound, "The provider was not found.");
        if (expectedRevision != current.Revision)
            return Result<ProviderRegistration>.Failure(VersionedRecordRules.StaleRevision("provider", current.Revision).ToRequestError());
        if (Validate(providerId, requestId, content) is { } error)
            return Result<ProviderRegistration>.Failure(error);
        var ev = new ProviderRevised(_tenantId, providerId, requestId, current.Revision + 1, content, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.ProviderRevised).Length > MaximumPayloadBytes)
            return Failure(RequestErrorKind.Validation, "The provider declaration exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<ProviderRegistration>.Success(new ProviderRegistration(providerId, ev.Revision));
    }

    RequestError? Validate(Uuid providerId, Uuid requestId, ProviderContent content)
    {
        if (providerId == Uuid.Empty || requestId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation, "A provider declaration requires nonempty identities.");
        if (ProviderRules.Validate(content) is { } error)
            return new RequestError(RequestErrorKind.Validation, error);
        return _providers.Values.Any(provider => provider.ProviderId != providerId &&
            StringComparer.OrdinalIgnoreCase.Equals(provider.Content.Name, content.Name))
            ? new RequestError(RequestErrorKind.Conflict, "An active provider already uses this governed name.")
            : null;
    }

    void Apply(Uuid tenantId, Uuid providerId, Uuid requestId, long revision,
        ProviderContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (tenantId != _tenantId || revision != (_providers.GetValueOrDefault(providerId)?.Revision ?? 0) + 1)
            throw new InvalidOperationException("A provider declaration must follow its tenant and previous revision.");
        content = ProviderRules.Normalize(content);
        _providers[providerId] = new ProviderView(_tenantId, providerId, revision, content,
            "manual", "active", ProviderRules.Unresolved(content), actor, recordedAt);
        _decisions.Add(requestId, (providerId, revision == 1 ? null : revision - 1, content, revision));
    }

    static bool SameIntent(ProviderContent left, ProviderContent right) =>
        left with { MaterialityBasis = null, Dependencies = null, OwnerPersonRevision = null } ==
        right with { MaterialityBasis = null, Dependencies = null, OwnerPersonRevision = null } &&
        left.MaterialityBasis!.SequenceEqual(right.MaterialityBasis!) &&
        left.Dependencies!.Select(static dependency => dependency with { SourceRevision = null, ProgramRevision = null, ApplicationRevision = null })
            .SequenceEqual(right.Dependencies!.Select(static dependency => dependency with { SourceRevision = null, ProgramRevision = null, ApplicationRevision = null }));

    static Result<ProviderRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<ProviderRegistration>.Failure(new RequestError(kind, message));
}
