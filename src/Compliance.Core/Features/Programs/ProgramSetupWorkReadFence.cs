using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Captures the input-projection watermarks that a derived setup-work read fences.</summary>
public sealed record ProgramSetupWorkReadFence(ProjectionCheckpoint Program,
    ProjectionCheckpoint Boundary);
