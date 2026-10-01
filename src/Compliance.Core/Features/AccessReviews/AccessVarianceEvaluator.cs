namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Explains an accepted population against the approved expectations effective at its
///     observation. It categorizes and explains; it never records or implies a review decision.
/// </summary>
public static class AccessVarianceEvaluator
{
    public const string Expected = "expected";
    public const string Unexpected = "unexpected";
    public const string Prohibited = "prohibited";
    public const string Missing = "missing";
    public const string Unresolved = "unresolved";
    public const string Excepted = "excepted";

    public static IReadOnlyList<AccessVarianceItemView> Evaluate(AccessPopulationFacts facts,
        IReadOnlyList<EffectiveAccessView> effective, Func<string, string> classificationOf,
        IReadOnlyList<AccessExpectationView> expectations,
        IReadOnlyList<AccessExpectationExceptionView> exceptions, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(classificationOf);
        var principals = facts.Principals.ToDictionary(static principal => principal.ProviderSubjectId,
            StringComparer.Ordinal);
        var entitlements = facts.Entitlements.ToDictionary(
            static entitlement => entitlement.ProviderEntitlementId, StringComparer.Ordinal);
        var active = exceptions.Where(exception => observedAt < exception.ExpiresAt).ToArray();
        var items = new List<AccessVarianceItemView>();
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in effective)
        {
            covered.Add(row.ProviderSubjectId);
            var principal = principals[row.ProviderSubjectId];
            var entitlement = entitlements[row.ProviderEntitlementId];
            var findings = new List<AccessVarianceFinding>();
            var privileged = false;
            var expected = false;
            var prohibited = false;
            var excepted = false;
            foreach (var expectation in expectations)
            {
                var parameters = expectation.Parameters;
                switch (expectation.RuleKind)
                {
                    case AccessReviewVocabulary.ForbiddenPrincipalKind
                        when parameters.PrincipalKind == principal.PrincipalKind:
                        Violation(expectation, "forbidden_principal_kind",
                            $"Principals of kind {principal.PrincipalKind} are prohibited.");
                        break;
                    case AccessReviewVocabulary.PrivilegedEntitlement
                        when Matches(parameters, principal, entitlement):
                        privileged = true;
                        findings.Add(new("privileged", expectation.ExpectationId, null,
                            "The entitlement is privileged and requires a named reviewer decision."));
                        break;
                    case AccessReviewVocabulary.RequiresExpiry
                        when Matches(parameters, principal, entitlement) && row.ExpiresAt is null:
                        Violation(expectation, "expiry_missing",
                            "The access requires an expiry and at least one path has none.");
                        break;
                    case AccessReviewVocabulary.RequiredAccess
                        when parameters.ProviderSubjectId == row.ProviderSubjectId &&
                             parameters.ProviderEntitlementId == row.ProviderEntitlementId:
                        expected = true;
                        findings.Add(new("required_access", expectation.ExpectationId, null,
                            "The access is required by an approved expectation."));
                        break;
                }
            }
            var classification = classificationOf(row.ProviderSubjectId);
            if (classification == AccessReviewVocabulary.Unclassified)
                findings.Add(new("unclassified_subject", null, null,
                    "The subject has no explicit human, nhi, or shared classification."));
            var category = classification == AccessReviewVocabulary.Unclassified ? Unresolved
                : prohibited ? Prohibited
                : excepted ? Excepted
                : expected ? Expected
                : Unexpected;
            if (category == Unexpected)
                findings.Add(new("not_covered", null, null,
                    "No approved expectation requires this access."));
            items.Add(new AccessVarianceItemView(category, row.ProviderSubjectId,
                row.ProviderEntitlementId, privileged, findings));

            void Violation(AccessExpectationView expectation, string kind, string explanation)
            {
                var exception = active.FirstOrDefault(candidate =>
                    candidate.ExpectationId == expectation.ExpectationId &&
                    candidate.ProviderSubjectId == row.ProviderSubjectId &&
                    (candidate.ProviderEntitlementId is null ||
                     candidate.ProviderEntitlementId == row.ProviderEntitlementId));
                if (exception is null)
                    prohibited = true;
                else
                    excepted = true;
                findings.Add(new(kind, expectation.ExpectationId, exception?.ExceptionId,
                    exception is null ? explanation : explanation + " An approved exception applies until " +
                        exception.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "."));
            }
        }

        foreach (var expectation in expectations)
        {
            var parameters = expectation.Parameters;
            if (expectation.RuleKind == AccessReviewVocabulary.RequiredAccess &&
                !effective.Any(row => row.ProviderSubjectId == parameters.ProviderSubjectId &&
                                      row.ProviderEntitlementId == parameters.ProviderEntitlementId))
                items.Add(new AccessVarianceItemView(Missing, parameters.ProviderSubjectId!,
                    parameters.ProviderEntitlementId, false,
                    [new("required_access", expectation.ExpectationId, null,
                        "Approved required access was not observed.")]));
            if (expectation.RuleKind == AccessReviewVocabulary.ForbiddenPrincipalKind)
            {
                foreach (var principal in facts.Principals.Where(principal =>
                             principal.PrincipalKind == parameters.PrincipalKind &&
                             !covered.Contains(principal.ProviderSubjectId)))
                {
                    var exception = active.FirstOrDefault(candidate =>
                        candidate.ExpectationId == expectation.ExpectationId &&
                        candidate.ProviderSubjectId == principal.ProviderSubjectId);
                    items.Add(new AccessVarianceItemView(exception is null ? Prohibited : Excepted,
                        principal.ProviderSubjectId, null, false,
                        [new("forbidden_principal_kind", expectation.ExpectationId,
                            exception?.ExceptionId,
                            $"Principals of kind {principal.PrincipalKind} are prohibited.")]));
                }
            }
        }
        return items;
    }

    static bool Matches(AccessExpectationParameters parameters, AccessPrincipalFact principal,
        AccessEntitlementFact entitlement) =>
        (parameters.PrincipalKind is null || parameters.PrincipalKind == principal.PrincipalKind) &&
        (parameters.EntitlementKind is null || parameters.EntitlementKind == entitlement.EntitlementKind) &&
        (parameters.ProviderEntitlementId is null ||
         parameters.ProviderEntitlementId == entitlement.ProviderEntitlementId);
}
