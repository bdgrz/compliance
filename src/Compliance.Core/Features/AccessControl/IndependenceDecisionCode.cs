namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A stable Compliance policy outcome for independence checks.</summary>
public enum IndependenceDecisionCode
{
    Allowed,
    PersonPracticeConflict,
    AssignmentInvalid,
    EvaluationInputInvalid,
    ServiceHistoryInvalid,
    RecentImpairingService,
    PartnerEvaluationRequired,
    RuleSetInvalid,
    ServiceNotClassified,
}
