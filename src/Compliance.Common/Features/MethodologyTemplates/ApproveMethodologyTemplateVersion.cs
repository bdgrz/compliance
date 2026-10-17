using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the approve methodology template version intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.approve-methodology-template-version", 1)]
public sealed record ApproveMethodologyTemplateVersion(Uuid TemplateId, long Version, string ApprovalRationale) : IRequest, IPlatformOperatorRequest, ICallable;
