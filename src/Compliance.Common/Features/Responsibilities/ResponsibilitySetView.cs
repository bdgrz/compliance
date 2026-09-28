using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed record ResponsibilitySetView(Uuid TenantId, Uuid SetId, ResponsibilityScope Scope, long Revision,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments);
