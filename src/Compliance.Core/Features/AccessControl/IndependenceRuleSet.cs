namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>An immutable version of the firm's service classifications and look-back period.</summary>
public sealed class IndependenceRuleSet
{
    public IndependenceRuleSet(long version, int lookBackMonths,
        IReadOnlyList<IndependenceServiceRule> serviceRules)
    {
        ArgumentNullException.ThrowIfNull(serviceRules);
        Version = version;
        LookBackMonths = lookBackMonths;
        ServiceRules = Array.AsReadOnly(serviceRules.ToArray());
    }

    public long Version { get; }
    public int LookBackMonths { get; }
    public IReadOnlyList<IndependenceServiceRule> ServiceRules { get; }

    public bool IsValid => Version > 0 && LookBackMonths > 0 &&
        ServiceRules.All(rule => rule is not null && !string.IsNullOrWhiteSpace(rule.ServiceType) &&
            Enum.IsDefined(rule.Classification) && Enum.IsDefined(rule.ManagementFunctionsClassification)) &&
        ServiceRules.Select(rule => rule.ServiceType).Distinct(StringComparer.Ordinal).Count() == ServiceRules.Count;

    public bool TryGetClassification(string serviceType, bool involvedManagementFunctions,
        out IndependenceServiceClassification classification)
    {
        classification = default;
        if (!IsValid || string.IsNullOrWhiteSpace(serviceType))
            return false;

        var matches = ServiceRules.Where(rule => rule is not null &&
                StringComparer.Ordinal.Equals(rule.ServiceType, serviceType))
            .ToArray();
        if (matches.Length != 1 || !Enum.IsDefined(matches[0].Classification))
            return false;
        classification = involvedManagementFunctions
            ? matches[0].ManagementFunctionsClassification
            : matches[0].Classification;
        return true;
    }
}
