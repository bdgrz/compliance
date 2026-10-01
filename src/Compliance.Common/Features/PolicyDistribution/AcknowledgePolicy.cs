using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A personal acknowledgement of the exact version, hash, and text shown. A member
///     acknowledges for themselves; a program manager may record it for a non-member with
///     separate attribution. HTTP-only: it is deliberately not an MCP tool.
/// </summary>
[Discriminator("bdgrz.policy.acknowledge", 1)]
public sealed record AcknowledgePolicy(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, long PolicyVersion, string ContentSha256, string AcknowledgementText)
    : IRequest<CampaignAcknowledgementView>, IProgramReadRequest, ICallable;
