using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Internal recovery command; personal acceptance must already be durable.</summary>
[Discriminator("bdgrz.application_import.execute", 1)]
public sealed record ExecuteApplicationImport(Uuid TenantId, Uuid BatchId) : IRequest;
