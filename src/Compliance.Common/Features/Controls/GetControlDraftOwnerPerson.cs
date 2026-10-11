using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.owner_person.get", 1)]
public sealed record GetControlDraftOwnerPerson(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlDraftOwnerPersonView>, IProgramScopedRequest, ICallable;
