using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.management-acknowledge", 1)]
public sealed record AcknowledgeEngagementManagement(Uuid TenantId, Uuid EngagementId, Uuid AcknowledgementId,
    long ExpectedSequence, long ExpectedEngagementRevision, IReadOnlyList<Uuid> CompleteServiceRecordIds, string Statement)
    : IRequest<EngagementManagementAcknowledgementView>, IIndependenceAdministrationRequest, ICallable;
