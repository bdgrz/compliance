using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Creates or revises one tenant-supplied licensed criteria entry.</summary>
[Discriminator("bdgrz.criteria.overlay.set", 1)]
public sealed record SetCriteriaTextOverlay(Uuid TenantId, Uuid EditionId, string Identifier,
    long? ExpectedRevision, CriteriaTextOverlayContent Content)
    : IRequest<CriteriaTextOverlayRegistration>, IProgramManagementRequest, ICallable;
