using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the propose deidentified template contribution intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.propose-deidentified-template-contribution", 1)]
public sealed record ProposeDeidentifiedTemplateContribution(Uuid TenantId, Uuid ContributionId, Uuid SourceRecordId, long SourceVersion, string ProposedContent, string DeidentificationRationale) : IRequest, IClientManagementMutationRequest, ICallable;
