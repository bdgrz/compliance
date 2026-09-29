namespace Bdgrz.Compliance.Features.Programs;

public interface IClientServiceProgramResourceRequest : IProgramResourceRequest
{
    Cntryl.Portia.Uuid ServiceId { get; }
    Cntryl.Portia.Uuid IProgramResourceRequest.ResourceId => ServiceId;
    ProgramResourceKind IProgramResourceRequest.ResourceKind => ProgramResourceKind.ClientService;
}

public interface IClientServiceProgramReadRequest : IClientServiceProgramResourceRequest,
    IProgramReadResourceRequest;
