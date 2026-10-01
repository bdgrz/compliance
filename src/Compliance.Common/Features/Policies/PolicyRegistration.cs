using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyRegistration(Uuid PolicyId, string Identifier, long Revision);
