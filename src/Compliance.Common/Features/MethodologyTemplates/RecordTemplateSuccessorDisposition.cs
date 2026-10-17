using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the record template successor disposition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.record-template-successor-disposition", 1)]
public sealed record RecordTemplateSuccessorDisposition(Uuid TenantId, Uuid ProgramId, Uuid TemplateId, long NewTemplateVersion, string Disposition, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
