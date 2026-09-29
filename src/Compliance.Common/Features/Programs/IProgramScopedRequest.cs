using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>A program-management request whose authorization must match one program scope.</summary>
public interface IProgramScopedRequest : IProgramManagementRequest
{
    const string ManagementPermission = "program.manage";

    Uuid ProgramId { get; }

    string RequiredPermission => ManagementPermission;
}

/// <summary>A read request scoped to one program and authorized by its read permission.</summary>
public interface IProgramReadRequest : IProgramScopedRequest
{
    const string ReadPermission = "tenant.access";

    string IProgramScopedRequest.RequiredPermission => ReadPermission;
}
