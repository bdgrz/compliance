using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Attributed, immutable advisor feedback on one gap of one exact assessment. Current is false
///     once a later assessment supersedes it. Feedback never satisfies a decision or approval.
/// </summary>
public sealed record ReadinessAnnotationView(Uuid AnnotationId, Uuid AssessmentId, Uuid GapId,
    string Body, Uuid AuthorMemberId, ActorReference AuthoredBy, DateTimeOffset AnnotatedAt,
    bool Current = true);
