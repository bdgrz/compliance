using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the resolve readiness material import row intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.resolve-readiness-material-import-row", 1)]
public sealed record ResolveReadinessMaterialImportRow(Uuid TenantId, Uuid ImportId, Uuid RowId, string Resolution, Uuid? MatchedRecordId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;
