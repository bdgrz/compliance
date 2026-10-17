using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the sign management representation letter intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.sign-management-representation-letter", 1)]
public sealed record SignManagementRepresentationLetter(Uuid TenantId, Uuid ProgramId, Uuid LetterId, Uuid SignerMemberId, Uuid SignedArtifactId, DateTimeOffset SignedAt) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
