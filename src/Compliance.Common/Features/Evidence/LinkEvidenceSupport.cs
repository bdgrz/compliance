using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Links an exact artifact to the program record and period it supports.</summary>
[Discriminator("bdgrz.evidence.support.link", 1)]
public sealed record LinkEvidenceSupport(Uuid TenantId, Uuid ProgramId, Uuid ArtifactId,
    string SupportedRecordType, Uuid SupportedRecordId, long SupportedRecordRevision,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Purpose, string SufficiencyRationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
