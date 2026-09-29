using Bdgrz.Compliance.Features.Programs;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface ISnapshotProgramResourceRequest : IProgramResourceRequest
{
    Cntryl.Portia.Uuid SnapshotId { get; }
    Cntryl.Portia.Uuid IProgramResourceRequest.ResourceId => SnapshotId;
    ProgramResourceKind IProgramResourceRequest.ResourceKind => ProgramResourceKind.Snapshot;
}

public interface ISnapshotProgramReadRequest : ISnapshotProgramResourceRequest,
    IProgramReadResourceRequest;
