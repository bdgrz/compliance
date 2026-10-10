using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An effective-dated retirement (M0-D05); a merge retains the approved successor snapshot revision.</summary>
public sealed record RetirementView(DateTimeOffset EffectiveAt, string Reason,
    Uuid? MergedIntoApplicationId, long? MergedIntoApplicationRevision = null);
