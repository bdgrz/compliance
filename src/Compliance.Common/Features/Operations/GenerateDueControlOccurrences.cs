using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the generate due control occurrences intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.generate-due-control-occurrences", 1)]
public sealed record GenerateDueControlOccurrences(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, DateOnly ThroughDate, string GenerationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest;
