using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlSuccessorRegistration(Uuid ControlId, Uuid DraftVersionId,
    Uuid PredecessorVersionId, long Revision);
