using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the approve management assertion intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.approve-management-assertion", 1)]
public sealed record ApproveManagementAssertion(Uuid TenantId, Uuid ProgramId, Uuid AssertionId, long Version, string Outcome, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
