using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     A program request that any member with program read access may submit. The handler and
///     aggregate then require the actor to hold the operating responsibility or manage the program.
/// </summary>
public interface IControlOperationRequest : IProgramScopedRequest, IClientManagementMutationRequest
{
    string IProgramScopedRequest.RequiredPermission => IProgramReadRequest.ReadPermission;
}
