using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record AccessGrantScope(AccessGrantScopeKind Kind, Uuid Id, string? ResourceType = null);
