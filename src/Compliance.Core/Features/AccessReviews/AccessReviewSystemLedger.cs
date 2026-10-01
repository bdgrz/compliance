using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One system instance's versioned access expectations, their expiring exceptions, and the
///     approved exceptions for a missing population. Every change is appended; nothing is edited.
/// </summary>
public sealed class AccessReviewSystemLedger : Aggregate
{
    public const string Area = "access-review-systems";
    public const string Proposed = "proposed";
    public const string Approved = "approved";

    readonly Uuid _tenantId;
    readonly List<AccessExpectationView> _expectations = [];
    readonly Dictionary<Uuid, Uuid> _proposers = [];
    readonly List<AccessExpectationExceptionView> _exceptions = [];
    readonly List<AccessPopulationExceptionView> _populationExceptions = [];

    public AccessReviewSystemLedger(Uuid tenantId, Uuid systemInstanceId)
        : base(systemInstanceId, new EventStreamAddress(tenantId.ToString(), Area,
            systemInstanceId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessExpectationProposed>(ev =>
        {
            Revision = ev.Revision;
            _expectations.Add(ev.Expectation);
            _proposers[ev.Expectation.ExpectationId] = ev.ProposerMemberId;
        });
        On<AccessExpectationApproved>(ev =>
        {
            Revision = ev.Revision;
            var index = _expectations.FindIndex(item => item.ExpectationId == ev.ExpectationId);
            _expectations[index] = _expectations[index] with
            {
                Status = Approved,
                ApprovedBy = ev.ApprovedBy,
                ApprovedAt = ev.ApprovedAt,
                SeparationOfDutiesWaiverId = ev.SeparationOfDutiesWaiverId,
            };
        });
        On<AccessExpectationExceptionRecorded>(ev =>
        {
            Revision = ev.Revision;
            _exceptions.Add(ev.Exception);
        });
        On<AccessPopulationExceptionRecorded>(ev =>
        {
            Revision = ev.Revision;
            _populationExceptions.Add(ev.Exception);
        });
    }

    public long Revision { get; private set; }
    public IReadOnlyList<AccessExpectationView> Expectations => _expectations;
    public IReadOnlyList<AccessExpectationExceptionView> Exceptions => _exceptions;
    public IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions => _populationExceptions;

    public AccessExpectationsView ToView() => new(_tenantId, Id, Revision, [.. _expectations],
        [.. _exceptions], [.. _populationExceptions]);

    /// <summary>
    ///     Approved expectations effective at <paramref name="at" />. An approved successor ends its
    ///     predecessor from the successor's effective date.
    /// </summary>
    public IReadOnlyList<AccessExpectationView> EffectiveAt(DateTimeOffset at) =>
        _expectations.Where(expectation => expectation.Status == Approved &&
                                           expectation.EffectiveFrom <= at &&
                                           (expectation.EffectiveUntil is null || at < expectation.EffectiveUntil) &&
                                           !_expectations.Any(successor =>
                                               successor.Status == Approved &&
                                               successor.SupersedesExpectationId == expectation.ExpectationId &&
                                               successor.EffectiveFrom <= at))
            .ToArray();

    public AccessPopulationExceptionView? PopulationExceptionAt(DateTimeOffset at) =>
        _populationExceptions.LastOrDefault(exception => exception.ApprovedAt <= at &&
                                                         at < exception.ExpiresAt);

    public Result<AccessExpectationView> Propose(long expectedRevision, Uuid expectationId,
        string ruleKind, AccessExpectationParameters parameters, string rationale,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, Uuid? supersedesExpectationId,
        Uuid proposerMemberId, ActorReference proposedBy, DateTimeOffset now)
    {
        if (_expectations.Find(item => item.ExpectationId == expectationId) is { } existing)
            return _proposers[expectationId] == proposerMemberId
                ? Result<AccessExpectationView>.Success(existing)
                : Failure<AccessExpectationView>(RequestErrorKind.Conflict,
                    "The expectation already exists.");
        if (expectedRevision != Revision)
            return Failure<AccessExpectationView>(RequestErrorKind.Conflict,
                $"The access-review ledger is at revision {Revision}; expected {expectedRevision}.");
        if (ValidateRule(ruleKind, parameters) is { } invalid)
            return Failure<AccessExpectationView>(RequestErrorKind.Validation, invalid);
        if (!AccessReviewVocabulary.IsBoundedText(rationale, 4000))
            return Failure<AccessExpectationView>(RequestErrorKind.Validation,
                "An expectation requires a rationale of at most 4000 characters.");
        if (effectiveUntil is { } until && until <= effectiveFrom)
            return Failure<AccessExpectationView>(RequestErrorKind.Validation,
                "An expectation must end after it takes effect.");
        if (supersedesExpectationId is { } predecessorId &&
            _expectations.Find(item => item.ExpectationId == predecessorId) is not
            { Status: Approved })
            return Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The superseded expectation was not found or is not approved.");
        var view = new AccessExpectationView(expectationId, Id, supersedesExpectationId, ruleKind,
            Normalize(parameters), rationale.Trim(), effectiveFrom, effectiveUntil, Proposed,
            proposedBy, now, null, null, null);
        RaiseEvent(new AccessExpectationProposed(_tenantId, Id, Revision + 1, proposerMemberId, view));
        return Result<AccessExpectationView>.Success(view);
    }

    /// <summary>
    ///     The proposer may approve only under an approved waiver scoped to
    ///     (access_expectation, expectation_id, expectation_id, 1, approve).
    /// </summary>
    public Result<AccessExpectationView> Approve(Uuid expectationId, long expectedRevision,
        Uuid approverMemberId, ActorReference approvedBy, DateTimeOffset now,
        SeparationOfDutiesWaiver? waiver)
    {
        var index = _expectations.FindIndex(item => item.ExpectationId == expectationId);
        if (index < 0)
            return Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The expectation was not found.");
        if (_expectations[index].Status == Approved)
            return _expectations[index].ApprovedBy?.Id == approvedBy.Id
                ? Result<AccessExpectationView>.Success(_expectations[index])
                : Failure<AccessExpectationView>(RequestErrorKind.Conflict,
                    "The expectation was already approved.");
        if (expectedRevision != Revision)
            return Failure<AccessExpectationView>(RequestErrorKind.Conflict,
                $"The access-review ledger is at revision {Revision}; expected {expectedRevision}.");
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.AccessExpectation,
            expectationId, expectationId, 1, SeparationOfDutiesActions.Approve);
        var conflict = _proposers[expectationId] == approverMemberId;
        if (waiver is not null && (!conflict || waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, approverMemberId, now)))
            return Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                "The separation-of-duties waiver is not active for this member and expectation.");
        if (conflict && waiver is null)
            return Failure<AccessExpectationView>(RequestErrorKind.Forbidden,
                "The member who proposed an expectation cannot approve it.");
        RaiseEvent(new AccessExpectationApproved(_tenantId, Id, Revision + 1, expectationId,
            approvedBy, now, waiver?.Id));
        return Result<AccessExpectationView>.Success(_expectations[index]);
    }

    public Result<AccessExpectationExceptionView> RecordException(Uuid exceptionId,
        Uuid expectationId, long expectedRevision, string providerSubjectId,
        string? providerEntitlementId, string rationale, DateTimeOffset expiresAt,
        ActorReference approvedBy, DateTimeOffset now)
    {
        if (_exceptions.Find(item => item.ExceptionId == exceptionId) is { } existing)
            return Result<AccessExpectationExceptionView>.Success(existing);
        if (_expectations.Find(item => item.ExpectationId == expectationId) is not { Status: Approved })
            return Failure<AccessExpectationExceptionView>(RequestErrorKind.NotFound,
                "The approved expectation was not found.");
        if (expectedRevision != Revision)
            return Failure<AccessExpectationExceptionView>(RequestErrorKind.Conflict,
                $"The access-review ledger is at revision {Revision}; expected {expectedRevision}.");
        if (!AccessReviewVocabulary.IsBoundedText(providerSubjectId, 512) ||
            providerEntitlementId is { Length: 0 or > 512 } ||
            !AccessReviewVocabulary.IsBoundedText(rationale, 4000))
            return Failure<AccessExpectationExceptionView>(RequestErrorKind.Validation,
                "An exception requires a principal, an optional entitlement, and a rationale of at most 4000 characters.");
        if (expiresAt <= now)
            return Failure<AccessExpectationExceptionView>(RequestErrorKind.Validation,
                "An exception must expire in the future.");
        var view = new AccessExpectationExceptionView(exceptionId, expectationId, providerSubjectId,
            providerEntitlementId, rationale.Trim(), expiresAt, approvedBy, now);
        RaiseEvent(new AccessExpectationExceptionRecorded(_tenantId, Id, Revision + 1, view));
        return Result<AccessExpectationExceptionView>.Success(view);
    }

    public Result<AccessPopulationExceptionView> RecordPopulationException(Uuid exceptionId,
        long expectedRevision, string reason, DateTimeOffset expiresAt, ActorReference approvedBy,
        DateTimeOffset now)
    {
        if (_populationExceptions.Find(item => item.ExceptionId == exceptionId) is { } existing)
            return Result<AccessPopulationExceptionView>.Success(existing);
        if (expectedRevision != Revision)
            return Failure<AccessPopulationExceptionView>(RequestErrorKind.Conflict,
                $"The access-review ledger is at revision {Revision}; expected {expectedRevision}.");
        if (!AccessReviewVocabulary.IsBoundedText(reason, 4000))
            return Failure<AccessPopulationExceptionView>(RequestErrorKind.Validation,
                "A population exception requires a reason of at most 4000 characters.");
        if (expiresAt <= now)
            return Failure<AccessPopulationExceptionView>(RequestErrorKind.Validation,
                "A population exception must expire in the future.");
        var view = new AccessPopulationExceptionView(exceptionId, Id, reason.Trim(), expiresAt,
            approvedBy, now);
        RaiseEvent(new AccessPopulationExceptionRecorded(_tenantId, Id, Revision + 1, view));
        return Result<AccessPopulationExceptionView>.Success(view);
    }

    static string? ValidateRule(string ruleKind, AccessExpectationParameters? parameters)
    {
        if (parameters is null || !AccessReviewVocabulary.RuleKinds.Contains(ruleKind ?? string.Empty))
            return "The rule kind must be forbidden_principal_kind, privileged_entitlement, requires_expiry, or required_access.";
        if (parameters.PrincipalKind is { } kind && !AccessReviewVocabulary.PrincipalKinds.Contains(kind))
            return "The principal kind is not recognized.";
        if (parameters.EntitlementKind is { } entitlementKind &&
            !AccessReviewVocabulary.EntitlementKinds.Contains(entitlementKind))
            return "The entitlement kind is not recognized.";
        if (parameters.ProviderEntitlementId is { Length: 0 or > 512 } ||
            parameters.ProviderSubjectId is { Length: 0 or > 512 })
            return "Provider IDs must be between 1 and 512 characters.";
        var entitlementMatch = parameters.EntitlementKind is not null ||
                               parameters.ProviderEntitlementId is not null;
        return ruleKind switch
        {
            AccessReviewVocabulary.ForbiddenPrincipalKind when parameters.PrincipalKind is null ||
                                                                entitlementMatch ||
                                                                parameters.ProviderSubjectId is not null =>
                "A forbidden_principal_kind rule names only a principal kind.",
            AccessReviewVocabulary.PrivilegedEntitlement or AccessReviewVocabulary.RequiresExpiry
                when !entitlementMatch || parameters.ProviderSubjectId is not null =>
                "This rule names an entitlement kind or provider entitlement ID, optionally narrowed by principal kind.",
            AccessReviewVocabulary.RequiredAccess when parameters.ProviderSubjectId is null ||
                                                        parameters.ProviderEntitlementId is null ||
                                                        parameters.EntitlementKind is not null ||
                                                        parameters.PrincipalKind is not null =>
                "A required_access rule names exactly one provider subject and provider entitlement.",
            _ => null,
        };
    }

    static AccessExpectationParameters Normalize(AccessExpectationParameters parameters) =>
        new(parameters.PrincipalKind, parameters.EntitlementKind, parameters.ProviderEntitlementId,
            parameters.ProviderSubjectId);

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
