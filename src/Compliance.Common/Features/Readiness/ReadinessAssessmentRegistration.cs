using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed record ReadinessAssessmentRegistration(Uuid AssessmentId, long Revision);
