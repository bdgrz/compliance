using Bdgrz.Compliance.Features.Programs;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryProgramResourceRequest : IProgramResourceRequest
{
    Cntryl.Portia.Uuid BoundaryId { get; }
    Cntryl.Portia.Uuid IProgramResourceRequest.ResourceId => BoundaryId;
    ProgramResourceKind IProgramResourceRequest.ResourceKind => ProgramResourceKind.Boundary;
}

public interface IBoundaryProgramReadRequest : IBoundaryProgramResourceRequest, IProgramReadResourceRequest;
