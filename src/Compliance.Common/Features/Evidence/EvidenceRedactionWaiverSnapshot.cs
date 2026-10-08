using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Exact bounded SoD exception provenance, without the administrative rationale.</summary>
public sealed record EvidenceRedactionWaiverSnapshot(Uuid TenantId, Uuid WaiverId,
    SeparationOfDutiesWaiverScope Scope, Uuid BeneficiaryMemberId, Uuid RequesterMemberId,
    Uuid ApproverMemberId, DateTimeOffset RequestedAt, DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt);
