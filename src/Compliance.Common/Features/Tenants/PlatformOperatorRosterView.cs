using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record PlatformOperatorRosterView(IReadOnlyList<Uuid> UserIds);
