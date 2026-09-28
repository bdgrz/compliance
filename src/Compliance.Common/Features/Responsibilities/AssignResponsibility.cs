using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibility.assign", 1)]
public sealed record AssignResponsibility(Uuid TenantId, Uuid MemberUserId,
    ResponsibilityType Type, string RecordType, Uuid RecordId, Uuid VersionId, long ScopeRevision,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    IReadOnlyList<Uuid> SeparationOfDutiesWaiverIds) : IRequest, IRbacManagementRequest, ICallable
{
    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
