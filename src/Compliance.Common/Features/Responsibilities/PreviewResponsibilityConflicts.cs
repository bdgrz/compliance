using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibilities.conflicts.preview", 1)]
public sealed record PreviewResponsibilityConflicts(Uuid TenantId, Uuid MemberUserId,
    ResponsibilityType Type, string RecordType, Uuid RecordId, Uuid VersionId,
    long ScopeRevision, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil)
    : IRequest<ResponsibilityConflictPreview>, ITenantAccessRequest, ICallable
{
    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
