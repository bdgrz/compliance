using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the review system description intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.review-system-description", 1)]
public sealed record ReviewSystemDescription(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, string Outcome, string Comments) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
