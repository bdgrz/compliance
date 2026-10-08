using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibility.revoke", 1)]
public sealed record RevokeResponsibility(Uuid TenantId, string RecordType, Uuid RecordId,
    Uuid VersionId, long ScopeRevision, Uuid AssignmentId, string Reason)
    : IRequest, IRbacManagementRequest, IClientManagementMutationRequest, ICallable
{
    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
