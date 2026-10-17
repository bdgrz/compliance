using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the generate system description export intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.generate-system-description-export", 1)]
public sealed record GenerateSystemDescriptionExport(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, string Format, string ExportKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
