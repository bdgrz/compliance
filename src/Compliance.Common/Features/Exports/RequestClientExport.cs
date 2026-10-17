using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the request client export intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.exports.request-client-export", 1)]
public sealed record RequestClientExport(Uuid TenantId, Uuid ExportId, string Scope, string Format, string ExportKey) : IRequest, IClientManagementMutationRequest, ICallable;
