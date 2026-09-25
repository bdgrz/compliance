using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskRegistration(Uuid RiskId, string Identifier, long Revision);
