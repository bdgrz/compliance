using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the revise methodology template draft intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.revise-methodology-template-draft", 1)]
public sealed record ReviseMethodologyTemplateDraft(Uuid TemplateId, long ExpectedRevision, string Content, string ChangeNotes) : IRequest, IPlatformOperatorRequest, ICallable;
