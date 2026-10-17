using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the record management representation letter intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.record-management-representation-letter", 1)]
public sealed record RecordManagementRepresentationLetter(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid ArtifactId, string Source, DateOnly LetterDate, IReadOnlyList<Uuid> RequiredSignerIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
