using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record AccessGrantSetView(Uuid TenantId, long Revision, IReadOnlyList<AccessGrantView> Grants);
