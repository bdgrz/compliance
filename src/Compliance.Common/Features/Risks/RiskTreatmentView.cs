using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskTreatmentView(string Kind, string Rationale, ActorReference ChosenBy,
    DateTimeOffset ChosenAt);
