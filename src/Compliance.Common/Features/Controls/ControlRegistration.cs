using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlRegistration(Uuid ControlId, string Identifier, long Revision);
