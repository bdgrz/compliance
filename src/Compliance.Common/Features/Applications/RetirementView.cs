using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An effective-dated retirement (M0-D05); <c>merged_into</c> names a successor application.</summary>
public sealed record RetirementView(DateTimeOffset EffectiveAt, string Reason,
    Uuid? MergedIntoApplicationId);
