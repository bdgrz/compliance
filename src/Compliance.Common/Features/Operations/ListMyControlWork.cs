using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Lists the acting member's operating responsibilities and due or upcoming work.</summary>
[Discriminator("bdgrz.control.work.mine", 1)]
public sealed record ListMyControlWork(Uuid TenantId, Uuid ProgramId, int? HorizonDays = null)
    : IRequest<MyControlWorkView>, IProgramReadRequest, ICallable;
