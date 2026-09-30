using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlRetirementRegistration(Uuid ControlId, Uuid RetirementId,
    Uuid VersionId, long Revision);
