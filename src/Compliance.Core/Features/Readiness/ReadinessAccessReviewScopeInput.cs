using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The recorded access-review scope history for one in-boundary system instance.</summary>
public sealed record ReadinessAccessReviewScopeInput(Uuid SystemInstanceId,
    AccessReviewScopeRecord? Scope);
