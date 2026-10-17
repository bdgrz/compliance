using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the record system description no change decision intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.record-system-description-no-change-decision", 1)]
public sealed record RecordSystemDescriptionNoChangeDecision(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, Uuid ChangeId, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
