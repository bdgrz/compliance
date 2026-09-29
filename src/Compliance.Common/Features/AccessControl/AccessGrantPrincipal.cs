using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record AccessGrantPrincipal(AccessGrantPrincipalKind Kind, Uuid Id);
