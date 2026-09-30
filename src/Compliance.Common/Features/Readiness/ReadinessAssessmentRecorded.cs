using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.assessment.recorded", 1)]
public sealed record ReadinessAssessmentRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    Uuid AssessmentId, string RuleVersion, DateTimeOffset AsOf, Uuid? EditionId,
    string InputFingerprint, IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
    Uuid RunnerMemberId, ActorReference RunBy, DateTimeOffset RunAt) : DomainEvent;
