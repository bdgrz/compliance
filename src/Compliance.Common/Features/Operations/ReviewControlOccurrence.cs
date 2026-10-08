using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The reviewer's personal, independent decision on the latest attestation; HTTP-only. The
///     performer or recorder cannot review it without an approved waiver.
/// </summary>
[Discriminator("bdgrz.control.occurrence.review", 1)]
public sealed record ReviewControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, Uuid AttestationId, string Outcome,
    string Rationale, IReadOnlyList<string>? RequestedActions = null,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
