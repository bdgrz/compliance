using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>A request authorized through the program that owns an existing resource.</summary>
public interface IProgramResourceRequest : IProgramManagementRequest
{
    Uuid ResourceId { get; }
    ProgramResourceKind ResourceKind { get; }

    string RequiredPermission => IProgramScopedRequest.ManagementPermission;
}

/// <summary>A read request authorized through the program that owns an existing resource.</summary>
public interface IProgramReadResourceRequest : IProgramResourceRequest
{
    string IProgramResourceRequest.RequiredPermission => IProgramReadRequest.ReadPermission;
}
