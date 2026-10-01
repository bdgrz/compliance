using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One manually attested access population for one exact system-instance revision (M0-D07).
///     Facts are replaced only while it is a draft; acceptance freezes them as an immutable
///     snapshot. Classification decisions are appended per accepted principal and never edited.
/// </summary>
public sealed class AccessPopulation : Aggregate
{
    public const string Area = "access-populations";
    public const string Draft = "draft";
    public const string Accepted = "accepted";

    static readonly AccessPopulationFacts NoFacts = new([], [], [], []);

    readonly Uuid _tenantId;
    readonly Dictionary<string, List<AccessPrincipalClassificationView>> _classifications =
        new(StringComparer.Ordinal);

    public AccessPopulation(Uuid tenantId, Uuid populationId)
        : base(populationId, new EventStreamAddress(tenantId.ToString(), Area,
            populationId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessPopulationOpened>(ev =>
        {
            Opened = ev;
            Revision = 1;
        });
        On<AccessPopulationFactsRecorded>(ev =>
        {
            Facts = ev.Facts;
            Revision = ev.Revision;
        });
        On<AccessPopulationAccepted>(ev =>
        {
            Acceptance = ev;
            Revision = ev.Revision;
        });
        On<AccessPrincipalClassified>(ev =>
        {
            if (!_classifications.TryGetValue(ev.ProviderSubjectId, out var history))
                _classifications[ev.ProviderSubjectId] = history = [];
            history.Add(ev.Decision);
        });
    }

    public AccessPopulationOpened? Opened { get; private set; }
    public AccessPopulationAccepted? Acceptance { get; private set; }
    public AccessPopulationFacts Facts { get; private set; } = NoFacts;
    public long Revision { get; private set; }
    public bool IsOpened => Opened is not null;
    public bool IsAccepted => Acceptance is not null;
    public string Status => IsAccepted ? Accepted : Draft;

    public IReadOnlyList<AccessPrincipalClassificationView> History(string providerSubjectId) =>
        _classifications.GetValueOrDefault(providerSubjectId) ?? [];

    public AccessPrincipalClassificationView? CurrentClassification(string providerSubjectId) =>
        _classifications.GetValueOrDefault(providerSubjectId) is { Count: > 0 } history
            ? history[^1]
            : null;

    public Result<AccessPopulationRegistration> Open(Uuid applicationId, Uuid systemInstanceId,
        long systemInstanceRevision, DateTimeOffset observedAt, string source, ActorReference actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Opened is { } opened)
            return opened.ApplicationId == applicationId &&
                   opened.SystemInstanceId == systemInstanceId &&
                   opened.SystemInstanceRevision == systemInstanceRevision &&
                   opened.ObservedAt == observedAt && opened.Source == source?.Trim()
                ? Result<AccessPopulationRegistration>.Success(new(Id, Revision))
                : Failure<AccessPopulationRegistration>(RequestErrorKind.Conflict,
                    "The population already exists with different content.");
        if (!AccessReviewVocabulary.IsBoundedText(source, 2000))
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "A population requires a source description of at most 2000 characters.");
        if (observedAt > now)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "A population cannot be observed in the future.");
        RaiseEvent(new AccessPopulationOpened(_tenantId, Id, applicationId, systemInstanceId,
            systemInstanceRevision, observedAt, AccessReviewVocabulary.ManualAttestation,
            source.Trim(), actor, now));
        return Result<AccessPopulationRegistration>.Success(new(Id, Revision));
    }

    public Result<AccessPopulationRegistration> Record(long expectedRevision,
        AccessPopulationFacts facts, ActorReference actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (Opened is null)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.NotFound,
                "The population was not found.");
        if (IsAccepted)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Conflict,
                "An accepted population is immutable.");
        if (expectedRevision != Revision)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Conflict,
                $"The population is at revision {Revision}; expected {expectedRevision}.");
        if (facts.Principals is null || facts.Entitlements is null || facts.GroupMembers is null ||
            facts.Assignments is null)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                "Every fact list is required; send an empty list when the source has none.");
        if (AccessPopulationAnalysis.FactCount(facts) > AccessPopulationAnalysis.MaximumFacts)
            return Failure<AccessPopulationRegistration>(RequestErrorKind.Validation,
                $"A population currently supports at most {AccessPopulationAnalysis.MaximumFacts} facts.");
        RaiseEvent(new AccessPopulationFactsRecorded(_tenantId, Id, Revision + 1, facts, actor, now));
        return Result<AccessPopulationRegistration>.Success(new(Id, Revision));
    }

    public Result<AccessPopulationAcceptance> Accept(long expectedRevision, Uuid snapshotId,
        string contentSha256, Uuid calculationId, string attestation, ActorReference actor,
        DateTimeOffset now)
    {
        if (Opened is null)
            return Failure<AccessPopulationAcceptance>(RequestErrorKind.NotFound,
                "The population was not found.");
        if (Acceptance is { } accepted)
            return accepted.SnapshotId == snapshotId
                ? Result<AccessPopulationAcceptance>.Success(new(Id, accepted.SnapshotId,
                    accepted.ContentSha256, accepted.CalculationId))
                : Failure<AccessPopulationAcceptance>(RequestErrorKind.Conflict,
                    "The population was already accepted.");
        if (expectedRevision != Revision)
            return Failure<AccessPopulationAcceptance>(RequestErrorKind.Conflict,
                $"The population is at revision {Revision}; expected {expectedRevision}.");
        if (AccessPopulationAnalysis.Validate(Facts).Count > 0)
            return Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                "Resolve every population issue before acceptance.");
        if (!AccessReviewVocabulary.IsBoundedText(attestation, 4000))
            return Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                "Acceptance requires an attestation of at most 4000 characters.");
        RaiseEvent(new AccessPopulationAccepted(_tenantId, Id, Revision + 1, snapshotId,
            contentSha256, calculationId, attestation.Trim(), actor, now));
        return Result<AccessPopulationAcceptance>.Success(new(Id, snapshotId, contentSha256,
            calculationId));
    }

    /// <summary>
    ///     Appends an explicit classification. Human requires a person, NHI a governed service
    ///     identity, and shared an accountable owner and justification (M0-D07).
    /// </summary>
    public Result<AccessPrincipalClassificationView> Classify(string providerSubjectId,
        long expectedClassificationCount, string classification, string rationale,
        Uuid? personId, Uuid? serviceIdentityId, Uuid? accountableOwnerPersonId,
        string? sharedJustification, string? proposedClassification, ActorReference actor,
        DateTimeOffset now)
    {
        if (!IsAccepted)
            return Failure<AccessPrincipalClassificationView>(RequestErrorKind.Conflict,
                "Only an accepted population can be classified.");
        var principal = Facts.Principals.FirstOrDefault(fact =>
            fact.ProviderSubjectId == providerSubjectId);
        if (principal is null)
            return Failure<AccessPrincipalClassificationView>(RequestErrorKind.NotFound,
                "The principal was not found in the accepted population.");
        if (AccessReviewVocabulary.IsAccessStructure(principal.PrincipalKind))
            return Failure<AccessPrincipalClassificationView>(RequestErrorKind.Validation,
                "Groups and roles are access structures and are not classified.");
        var history = History(providerSubjectId);
        if (expectedClassificationCount != history.Count)
            return Failure<AccessPrincipalClassificationView>(RequestErrorKind.Conflict,
                $"The principal has {history.Count} classification decisions; expected {expectedClassificationCount}.");
        var failure = ValidateClassification(classification, rationale, personId,
            serviceIdentityId, accountableOwnerPersonId, sharedJustification);
        if (failure is not null)
            return Result<AccessPrincipalClassificationView>.Failure(failure);
        var decision = new AccessPrincipalClassificationView(history.Count + 1, classification,
            personId, serviceIdentityId, accountableOwnerPersonId, sharedJustification?.Trim(),
            rationale.Trim(), proposedClassification, actor, now);
        RaiseEvent(new AccessPrincipalClassified(_tenantId, Id, providerSubjectId, decision));
        return Result<AccessPrincipalClassificationView>.Success(decision);
    }

    static RequestError? ValidateClassification(string classification, string rationale,
        Uuid? personId, Uuid? serviceIdentityId, Uuid? ownerPersonId, string? justification)
    {
        if (!AccessReviewVocabulary.Classifications.Contains(classification ?? string.Empty))
            return new(RequestErrorKind.Validation,
                "The classification must be human, nhi, shared, or unclassified.");
        if (!AccessReviewVocabulary.IsBoundedText(rationale, 2000))
            return new(RequestErrorKind.Validation,
                "A classification requires a rationale of at most 2000 characters.");
        var valid = classification switch
        {
            AccessReviewVocabulary.Human => personId is not null && serviceIdentityId is null &&
                                            ownerPersonId is null && justification is null,
            AccessReviewVocabulary.Nhi => serviceIdentityId is not null && personId is null &&
                                          ownerPersonId is null && justification is null,
            AccessReviewVocabulary.Shared => ownerPersonId is not null && personId is null &&
                                             serviceIdentityId is null &&
                                             AccessReviewVocabulary.IsBoundedText(justification, 2000),
            _ => personId is null && serviceIdentityId is null && ownerPersonId is null &&
                 justification is null,
        };
        return valid
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "Human requires only a person, nhi only a service identity, shared an accountable owner and justification, and unclassified none.");
    }

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
