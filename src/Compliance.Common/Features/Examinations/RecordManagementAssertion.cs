using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the record management assertion intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.record-management-assertion", 1)]
public sealed record RecordManagementAssertion(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid AssertionId, long PackageVersion, long DescriptionVersion, string AssertionText, IReadOnlyList<Uuid> SignerMemberIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
