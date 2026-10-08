using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public interface IEvidenceRedactionMutationRequest : IClientManagementMutationRequest
{
    Uuid RedactionId { get; }
}
