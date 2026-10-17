using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the bind system description sources intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.bind-system-description-sources", 1)]
public sealed record BindSystemDescriptionSources(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long ExpectedRevision, IReadOnlyList<Uuid> SourceVersionIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
