namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The aggregate compatibility result of evaluating recent nonattest services. A recorded partner
///     evaluation can allow acceptance of a conditionally compatible result without changing that result.
/// </summary>
public enum IndependenceEvaluationOutcome
{
    Compatible,
    ConditionallyCompatible,
    Impaired,
}
