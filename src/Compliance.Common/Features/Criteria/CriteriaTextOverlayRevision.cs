using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Retained supplied source content. HTTP/MCP output must pass through the usage policy.</summary>
public sealed record CriteriaTextOverlayRevision(Uuid TenantId, Uuid EditionId, string Identifier,
    long Revision, CriteriaTextOverlayContent Content, ActorReference Actor, DateTimeOffset RecordedAt);
